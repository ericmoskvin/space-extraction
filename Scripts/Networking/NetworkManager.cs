using System;
using System.Collections.Generic;
using Godot;
using SpaceExtraction.Characters;

namespace SpaceExtraction.Networking;

/// <summary>
/// Core network session manager for Milestone 0.
/// Encapsulates ENetMultiplayerPeer lifecycle, player registration RPCs,
/// and connection state events.
/// </summary>
public partial class NetworkManager : Node
{
    public const int DefaultPort = 7777;
    public const int MaxPlayers = 8;

    private readonly Dictionary<long, PlayerIdentity> _players = new();

    public bool IsOnline => Multiplayer.HasMultiplayerPeer() &&
                            Multiplayer.MultiplayerPeer is not OfflineMultiplayerPeer &&
                            Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;

    public bool IsConnecting => Multiplayer.HasMultiplayerPeer() &&
                                Multiplayer.MultiplayerPeer is not OfflineMultiplayerPeer &&
                                Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connecting;

    public bool IsDedicatedServer { get; private set; }

    public long LocalPeerId => (Multiplayer.HasMultiplayerPeer() && Multiplayer.MultiplayerPeer is not OfflineMultiplayerPeer)
        ? Multiplayer.GetUniqueId()
        : 0;

    public string LocalPlayerName { get; set; } = "Pilot";

    public IReadOnlyDictionary<long, PlayerIdentity> Players => _players;

    public event Action? SessionStarted;
    public event Action<string>? SessionEnded;
    public event Action<PlayerIdentity>? PlayerRegistered;
    public event Action<long>? PlayerUnregistered;

    public override void _Ready()
    {
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
    }

    public override void _ExitTree()
    {
        Multiplayer.PeerConnected -= OnPeerConnected;
        Multiplayer.PeerDisconnected -= OnPeerDisconnected;
        Multiplayer.ConnectedToServer -= OnConnectedToServer;
        Multiplayer.ConnectionFailed -= OnConnectionFailed;
        Multiplayer.ServerDisconnected -= OnServerDisconnected;

        ShutdownSession("Node exited tree");
    }

    public Error StartHost(int port = DefaultPort, string playerName = "HostPilot")
    {
        ShutdownSession("Restarting host");

        var peer = new ENetMultiplayerPeer();
        Error err = peer.CreateServer(port, MaxPlayers);
        if (err != Error.Ok)
        {
            GD.PrintErr($"[NetworkManager] Failed to create server on port {port}: {err}");
            return err;
        }

        Multiplayer.MultiplayerPeer = peer;
        IsDedicatedServer = false;
        LocalPlayerName = playerName;

        SessionStarted?.Invoke();

        var hostIdentity = new PlayerIdentity(1, playerName, 1, PlayerIdentity.GetDeterministicColor(1));
        _players[1] = hostIdentity;
        PlayerRegistered?.Invoke(hostIdentity);

        GD.Print($"[NetworkManager] Host started on port {port} as '{playerName}' (Peer 1)");
        return Error.Ok;
    }

    public Error StartDedicatedServer(int port = DefaultPort)
    {
        ShutdownSession("Restarting dedicated server");

        var peer = new ENetMultiplayerPeer();
        Error err = peer.CreateServer(port, MaxPlayers);
        if (err != Error.Ok)
        {
            GD.PrintErr($"[NetworkManager] Failed to create dedicated server on port {port}: {err}");
            return err;
        }

        Multiplayer.MultiplayerPeer = peer;
        IsDedicatedServer = true;

        SessionStarted?.Invoke();
        GD.Print($"[NetworkManager] Dedicated server started on port {port}");
        return Error.Ok;
    }

    public Error JoinServer(string address, int port = DefaultPort, string playerName = "ClientPilot")
    {
        ShutdownSession("Restarting client connection");

        var peer = new ENetMultiplayerPeer();
        Error err = peer.CreateClient(address, port);
        if (err != Error.Ok)
        {
            GD.PrintErr($"[NetworkManager] Failed to create client targeting {address}:{port}: {err}");
            return err;
        }

        Multiplayer.MultiplayerPeer = peer;
        IsDedicatedServer = false;
        LocalPlayerName = playerName;

        GD.Print($"[NetworkManager] Client connecting to {address}:{port} as '{playerName}'...");
        return Error.Ok;
    }

    public void ShutdownSession(string reason = "Disconnected")
    {
        bool wasActive = Multiplayer.HasMultiplayerPeer() && Multiplayer.MultiplayerPeer is not OfflineMultiplayerPeer;

        if (Multiplayer.MultiplayerPeer is ENetMultiplayerPeer enetPeer)
        {
            enetPeer.Close();
        }

        Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
        _players.Clear();
        IsDedicatedServer = false;

        if (wasActive)
        {
            GD.Print($"[NetworkManager] Session ended: {reason}");
            SessionEnded?.Invoke(reason);
        }
    }

    private void OnPeerConnected(long peerId)
    {
        GD.Print($"[NetworkManager] Peer connected: {peerId}");
    }

    private void OnPeerDisconnected(long peerId)
    {
        GD.Print($"[NetworkManager] Peer disconnected: {peerId}");

        if (Multiplayer.IsServer() && _players.ContainsKey(peerId))
        {
            Rpc(MethodName.SyncPlayerUnregisteredRpc, peerId);
        }
    }

    private void OnConnectedToServer()
    {
        GD.Print($"[NetworkManager] Connected to server as peer {Multiplayer.GetUniqueId()}");
        SessionStarted?.Invoke();
        RpcId(1, MethodName.RequestRegisterPlayerRpc, LocalPlayerName);
    }

    private void OnConnectionFailed()
    {
        GD.PrintErr("[NetworkManager] Connection to server failed.");
        ShutdownSession("Connection failed");
    }

    private void OnServerDisconnected()
    {
        GD.PrintErr("[NetworkManager] Authoritative server disconnected.");
        ShutdownSession("Server disconnected");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestRegisterPlayerRpc(string requestedName)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        long senderId = Multiplayer.GetRemoteSenderId();
        if (senderId <= 0 || _players.ContainsKey(senderId))
        {
            return;
        }

        // Send all already-registered players to the new client
        foreach (PlayerIdentity existing in _players.Values)
        {
            RpcId(senderId, MethodName.SyncPlayerRegisteredRpc, existing.PeerId, existing.DisplayName, existing.TeamId, existing.PlayerColor);
        }

        // Register and broadcast new player identity to all peers (including sender and server)
        int teamId = _players.Count + 1;
        var identity = new PlayerIdentity(senderId, requestedName, teamId, PlayerIdentity.GetDeterministicColor(senderId));
        Rpc(MethodName.SyncPlayerRegisteredRpc, identity.PeerId, identity.DisplayName, identity.TeamId, identity.PlayerColor);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SyncPlayerRegisteredRpc(long peerId, string displayName, int teamId, Color playerColor)
    {
        var identity = new PlayerIdentity(peerId, displayName, teamId, playerColor);
        _players[peerId] = identity;
        GD.Print($"[NetworkManager] Player registered: [{peerId}] {displayName} (Team {teamId})");
        PlayerRegistered?.Invoke(identity);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SyncPlayerUnregisteredRpc(long peerId)
    {
        if (_players.Remove(peerId))
        {
            GD.Print($"[NetworkManager] Player unregistered: {peerId}");
            PlayerUnregistered?.Invoke(peerId);
        }
    }
}
