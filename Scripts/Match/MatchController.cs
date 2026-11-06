using System;
using Godot;
using SpaceExtraction.Characters;
using SpaceExtraction.Networking;
using SpaceExtraction.Ships;
using SpaceExtraction.World;

namespace SpaceExtraction.Match;

/// <summary>
/// Authoritative match lifecycle controller for SpaceExtraction.
/// Coordinates match phase transitions, character and ship spawning/despawning,
/// and state replication.
/// </summary>
public partial class MatchController : Node
{
    public const int EnemyTargetShipId = 900;

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

        EnsureEnemyTargetShipSpawned();

        if (_networkManager != null && _gameWorld != null)
        {
            foreach (var kvp in _networkManager.Players)
            {
                long peerId = kvp.Key;
                PlayerIdentity identity = kvp.Value;
                Vector2 spawnPos = CalculateSpawnPosition(peerId);

                if (_gameWorld.GetPlayerCharacter(peerId) == null)
                {
                    Rpc(MethodName.SpawnPlayerCharacterRpc, identity.PeerId, identity.DisplayName, identity.TeamId, identity.PlayerColor, spawnPos);
                }

                if (_gameWorld.GetShip((int)peerId) == null)
                {
                    Rpc(MethodName.SpawnShipRpc,
                        (int)identity.PeerId,
                        $"{identity.DisplayName} Ship",
                        (int)ShipClassType.Corvette,
                        identity.TeamId,
                        identity.PeerId,
                        false,
                        identity.PlayerColor,
                        spawnPos,
                        0f);
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

    private void EnsureEnemyTargetShipSpawned()
    {
        if (_gameWorld == null)
        {
            return;
        }

        if (_gameWorld.GetShip(EnemyTargetShipId) == null)
        {
            Rpc(MethodName.SpawnShipRpc,
                EnemyTargetShipId,
                "Target Drone Corvette",
                (int)ShipClassType.Corvette,
                99,
                0L,
                true,
                new Color(1.0f, 0.35f, 0.3f),
                new Vector2(480f, 0f),
                Mathf.Pi);
        }
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

        // Sync all existing ships to the newly joined player
        foreach (Ship existingShip in _gameWorld.GetAllShips())
        {
            RpcId(identity.PeerId, MethodName.SpawnShipRpc,
                existingShip.ShipId,
                existingShip.ShipName,
                (int)existingShip.ShipClass,
                existingShip.OwnerTeamId,
                existingShip.PilotPeerId,
                existingShip.IsNpcTarget,
                existingShip.ShipColor,
                existingShip.GlobalPosition,
                existingShip.Rotation);

            if (existingShip.Hull != null)
            {
                existingShip.RpcId(identity.PeerId, Ship.MethodName.SyncHullStateRpc,
                    existingShip.Hull.CurrentHull,
                    existingShip.Hull.MaxHull,
                    existingShip.Hull.IsDisabled);
            }

            if (existingShip.Subsystems != null)
            {
                existingShip.RpcId(identity.PeerId, Ship.MethodName.SyncSubsystemsStateRpc,
                    existingShip.Subsystems.CurrentEngineHealth,
                    existingShip.Subsystems.CurrentWeaponsHealth,
                    existingShip.Subsystems.CurrentTurretHealth);
            }

            existingShip.RpcId(identity.PeerId, Ship.MethodName.SyncCrewRolesRpc,
                existingShip.PilotPeerId,
                existingShip.Turret.GunnerPeerId);

            if (existingShip.Interior != null)
            {
                existingShip.RpcId(identity.PeerId, Ship.MethodName.SyncDoorsStateRpc,
                    existingShip.Interior.GetDoorsBitmask());
            }
        }

        // Sync full ship interior occupancy and local position for all existing characters to the joining client
        foreach (PlayerCharacter existingChar in _gameWorld.GetAllPlayerCharacters())
        {
            if (existingChar.PeerId != identity.PeerId && existingChar.LocationContext.Mode == PlayerLocationMode.ShipInterior)
            {
                existingChar.RpcId(identity.PeerId, PlayerCharacter.MethodName.SyncShipOccupancyRpc,
                    existingChar.LocationContext.OccupiedShipId,
                    existingChar.LocationContext.IsPiloting,
                    existingChar.LocationContext.IsOperatingTurret,
                    existingChar.ShipLocalPosition);
            }
        }

        Vector2 spawnPos = CalculateSpawnPosition(identity.PeerId);

        // Spawn character for the new player across all peers
        if (_gameWorld.GetPlayerCharacter(identity.PeerId) == null)
        {
            Rpc(MethodName.SpawnPlayerCharacterRpc,
                identity.PeerId,
                identity.DisplayName,
                identity.TeamId,
                identity.PlayerColor,
                spawnPos);
        }

        // Spawn ship for the new player across all peers
        if (_gameWorld.GetShip((int)identity.PeerId) == null)
        {
            Rpc(MethodName.SpawnShipRpc,
                (int)identity.PeerId,
                $"{identity.DisplayName} Ship",
                (int)ShipClassType.Corvette,
                identity.TeamId,
                identity.PeerId,
                false,
                identity.PlayerColor,
                spawnPos,
                0f);
        }
    }

    private void OnPlayerUnregistered(long peerId)
    {
        if (Multiplayer.IsServer())
        {
            Rpc(MethodName.DespawnPlayerCharacterRpc, peerId);
            Rpc(MethodName.DespawnShipRpc, (int)peerId);

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
            foreach (Node child in _gameWorld.ShipsContainer.GetChildren())
            {
                child.QueueFree();
            }
            foreach (Node child in _gameWorld.ProjectilesContainer.GetChildren())
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

        PlayerCharacter? character = _gameWorld.GetPlayerCharacter(peerId);
        if (character == null)
        {
            var identity = new PlayerIdentity(peerId, displayName, teamId, playerColor);
            character = new PlayerCharacter();
            character.Initialize(identity, spawnPosition);
            _gameWorld.PlayersContainer.AddChild(character);
            GD.Print($"[MatchController] Spawned character for player {peerId} ({displayName}) at {spawnPosition}");
        }
        else
        {
            character.GlobalPosition = spawnPosition;
        }

        Ship? ship = _gameWorld.GetShipByPilot(peerId);
        if (ship != null)
        {
            character.SetShipOccupancy(ship.ShipId, true, ship.GetPath());
        }
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

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SpawnShipRpc(
        int shipId,
        string shipName,
        int shipClassInt,
        int ownerTeamId,
        long pilotPeerId,
        bool isNpcTarget,
        Color shipColor,
        Vector2 spawnPosition,
        float spawnRotation)
    {
        if (_gameWorld == null)
        {
            return;
        }

        Ship? ship = _gameWorld.GetShip(shipId);
        if (ship == null)
        {
            ship = new Ship();
            ship.Initialize(_gameWorld, shipId, shipName, (ShipClassType)shipClassInt, ownerTeamId, pilotPeerId, isNpcTarget, shipColor, spawnPosition, spawnRotation);
            _gameWorld.ShipsContainer.AddChild(ship);
            GD.Print($"[MatchController] Spawned ship {shipId} ({shipName}) at {spawnPosition}");
        }
        else
        {
            ship.GlobalPosition = spawnPosition;
            ship.Rotation = spawnRotation;
            if (pilotPeerId > 0)
            {
                ship.SetPilot(pilotPeerId);
            }
        }

        if (pilotPeerId > 0)
        {
            PlayerCharacter? pilot = _gameWorld.GetPlayerCharacter(pilotPeerId);
            pilot?.SetShipOccupancy(shipId, true, ship.GetPath());
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void DespawnShipRpc(int shipId)
    {
        Ship? ship = _gameWorld?.GetShip(shipId);
        if (ship != null)
        {
            GD.Print($"[MatchController] Despawning ship {shipId}");
            ship.QueueFree();
        }
    }

    private static Vector2 CalculateSpawnPosition(long peerId)
    {
        float angle = (peerId * 137.5f) * Mathf.Pi / 180.0f;
        float radius = 120.0f;
        return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
    }
}
