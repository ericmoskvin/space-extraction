using System;
using Godot;
using SpaceExtraction.Characters;
using SpaceExtraction.Networking;
using SpaceExtraction.World;

namespace SpaceExtraction.Match;

/// <summary>
/// Authoritative match lifecycle controller for Milestone 0.
/// Coordinates match phase transitions, character spawning/despawning,
/// and state replication.
/// </summary>
public partial class MatchController : Node
{
    private NetworkManager _networkManager = null!;
    private GameWorld _gameWorld = null!;

    public MatchPhase CurrentPhase { get; private set; } = MatchPhase.WaitingForPlayers;
    public double ElapsedMatchTime { get; private set; }

    public event Action<MatchPhase>? PhaseChanged;

    public void Initialize(NetworkManager networkManager, GameWorld gameWorld)
    {
        _networkManager = networkManager;
        _gameWorld = gameWorld;

        _networkManager.PlayerRegistered += OnPlayerRegistered;
        _networkManager.PlayerUnregistered += OnPlayerUnregistered;
        _networkManager.SessionEnded += OnSessionEnded;
        Multiplayer.PeerConnected += OnPeerConnected;
    }

    public override void _ExitTree()
    {
        if (_networkManager != null)
        {
            _networkManager.PlayerRegistered -= OnPlayerRegistered;
            _networkManager.PlayerUnregistered -= OnPlayerUnregistered;
            _networkManager.SessionEnded -= OnSessionEnded;
        }

        Multiplayer.PeerConnected -= OnPeerConnected;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (CurrentPhase == MatchPhase.InProgress)
        {
            ElapsedMatchTime += delta;
        }
    }

    public void StartMatch()
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        ElapsedMatchTime = 0.0;
        Rpc(MethodName.SyncMatchPhaseRpc, (int)MatchPhase.InProgress, 0.0);

        if (_networkManager != null && _gameWorld != null)
        {
            foreach (var kvp in _networkManager.Players)
            {
                long peerId = kvp.Key;
                PlayerIdentity identity = kvp.Value;
                if (_gameWorld.GetPlayerCharacter(peerId) == null)
                {
                    Vector2 spawnPos = CalculateSpawnPosition(peerId);
                    Rpc(MethodName.SpawnPlayerCharacterRpc, identity.PeerId, identity.DisplayName, identity.TeamId, identity.PlayerColor, spawnPos);
                }
            }
        }
    }

    public void EndMatch()
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        Rpc(MethodName.SyncMatchPhaseRpc, (int)MatchPhase.Ended, ElapsedMatchTime);
    }

    private void OnPlayerRegistered(PlayerIdentity identity)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        if (CurrentPhase == MatchPhase.WaitingForPlayers)
        {
            StartMatch();
            return;
        }

        // Sync current phase and elapsed time to the newly connected player
        RpcId(identity.PeerId, MethodName.SyncMatchPhaseRpc, (int)CurrentPhase, ElapsedMatchTime);

        // Sync all existing characters to the newly joined player
        foreach (PlayerCharacter existingChar in _gameWorld.GetAllPlayerCharacters())
        {
            if (existingChar.PeerId != identity.PeerId)
            {
                RpcId(identity.PeerId, MethodName.SpawnPlayerCharacterRpc,
                    existingChar.PeerId,
                    existingChar.DisplayName,
                    existingChar.TeamId,
                    existingChar.PlayerColor,
                    existingChar.GlobalPosition);
            }
        }

        // Spawn character for the new player across all peers
        if (_gameWorld.GetPlayerCharacter(identity.PeerId) == null)
        {
            Vector2 spawnPos = CalculateSpawnPosition(identity.PeerId);
            Rpc(MethodName.SpawnPlayerCharacterRpc,
                identity.PeerId,
                identity.DisplayName,
                identity.TeamId,
                identity.PlayerColor,
                spawnPos);
        }
    }

    private void OnPlayerUnregistered(long peerId)
    {
        if (Multiplayer.IsServer())
        {
            Rpc(MethodName.DespawnPlayerCharacterRpc, peerId);

            if (_networkManager != null && _networkManager.Players.Count == 0 && CurrentPhase != MatchPhase.WaitingForPlayers)
            {
                Rpc(MethodName.SyncMatchPhaseRpc, (int)MatchPhase.WaitingForPlayers, 0.0);
            }
        }
    }

    private void OnSessionEnded(string reason)
    {
        if (_gameWorld != null)
        {
            foreach (Node child in _gameWorld.PlayersContainer.GetChildren())
            {
                child.QueueFree();
            }
        }

        ElapsedMatchTime = 0.0;
        if (CurrentPhase != MatchPhase.WaitingForPlayers)
        {
            CurrentPhase = MatchPhase.WaitingForPlayers;
            PhaseChanged?.Invoke(CurrentPhase);
        }
    }

    private void OnPeerConnected(long peerId)
    {
        // Peer connected at transport layer
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SyncMatchPhaseRpc(int phaseInt, double elapsedTime)
    {
        var newPhase = (MatchPhase)phaseInt;
        ElapsedMatchTime = elapsedTime;

        if (CurrentPhase != newPhase)
        {
            CurrentPhase = newPhase;
            GD.Print($"[MatchController] Match phase transitioned to: {newPhase}");
            PhaseChanged?.Invoke(CurrentPhase);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SpawnPlayerCharacterRpc(long peerId, string displayName, int teamId, Color playerColor, Vector2 spawnPosition)
    {
        if (_gameWorld == null)
        {
            return;
        }

        PlayerCharacter? existing = _gameWorld.GetPlayerCharacter(peerId);
        if (existing != null)
        {
            existing.GlobalPosition = spawnPosition;
            return;
        }

        var identity = new PlayerIdentity(peerId, displayName, teamId, playerColor);
        var character = new PlayerCharacter();
        character.Initialize(identity, spawnPosition);
        _gameWorld.PlayersContainer.AddChild(character);
        GD.Print($"[MatchController] Spawned character for player {peerId} ({displayName}) at {spawnPosition}");
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void DespawnPlayerCharacterRpc(long peerId)
    {
        PlayerCharacter? character = _gameWorld?.GetPlayerCharacter(peerId);
        if (character != null)
        {
            GD.Print($"[MatchController] Despawning character for player {peerId}");
            character.QueueFree();
        }
    }

    private static Vector2 CalculateSpawnPosition(long peerId)
    {
        float angle = (peerId * 137.5f) * Mathf.Pi / 180.0f;
        float radius = 120.0f;
        return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
    }
}
