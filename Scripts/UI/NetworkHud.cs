using System;
using Godot;
using SpaceExtraction.Characters;
using SpaceExtraction.Match;
using SpaceExtraction.Networking;
using SpaceExtraction.World;

namespace SpaceExtraction.UI;

/// <summary>
/// Lightweight debug and connection HUD for Milestone 0.
/// Allows starting a host, dedicated server, or client, and displays authoritative match/player state.
/// </summary>
public partial class NetworkHud : CanvasLayer
{
    private NetworkManager _networkManager = null!;
    private MatchController _matchController = null!;
    private GameWorld _gameWorld = null!;

    private PanelContainer _panel = null!;
    private VBoxContainer _connectionBox = null!;
    private VBoxContainer _sessionBox = null!;

    private LineEdit _nameEdit = null!;
    private LineEdit _addressEdit = null!;
    private SpinBox _portSpinBox = null!;
    private Label _statusLabel = null!;
    private Label _sessionSummaryLabel = null!;
    private Label _playersListLabel = null!;

    private readonly System.Text.StringBuilder _hudStringBuilder = new();
    private double _hudUpdateAccumulator;

    public void Initialize(NetworkManager networkManager, MatchController matchController, GameWorld gameWorld)
    {
        _networkManager = networkManager;
        _matchController = matchController;
        _gameWorld = gameWorld;

        _networkManager.SessionStarted += OnSessionStateChanged;
        _networkManager.SessionEnded += OnSessionEnded;
        _networkManager.PlayerRegistered += OnPlayerListChanged;
        _networkManager.PlayerUnregistered += OnPlayerUnregistered;
        _matchController.PhaseChanged += OnMatchPhaseChanged;

        RefreshUiState();
    }

    public override void _ExitTree()
    {
        if (_networkManager != null)
        {
            _networkManager.SessionStarted -= OnSessionStateChanged;
            _networkManager.SessionEnded -= OnSessionEnded;
            _networkManager.PlayerRegistered -= OnPlayerListChanged;
            _networkManager.PlayerUnregistered -= OnPlayerUnregistered;
        }

        if (_matchController != null)
        {
            _matchController.PhaseChanged -= OnMatchPhaseChanged;
        }
    }

    public override void _Ready()
    {
        Layer = 10;

        _panel = new PanelContainer
        {
            Position = new Vector2(16f, 16f),
            CustomMinimumSize = new Vector2(360f, 0f)
        };
        AddChild(_panel);

        var rootVBox = new VBoxContainer();
        rootVBox.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(rootVBox);

        var titleLabel = new Label
        {
            Text = "2D SPACE EXTRACTION — MILESTONE 0",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        rootVBox.AddChild(titleLabel);

        _statusLabel = new Label
        {
            Text = "Status: Offline",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        rootVBox.AddChild(_statusLabel);

        // Connection controls container
        _connectionBox = new VBoxContainer();
        _connectionBox.AddThemeConstantOverride("separation", 6);
        rootVBox.AddChild(_connectionBox);

        _nameEdit = new LineEdit
        {
            Text = $"Pilot-{GD.Randi() % 900 + 100}",
            PlaceholderText = "Player Name"
        };
        _connectionBox.AddChild(_nameEdit);

        var addressRow = new HBoxContainer();
        _connectionBox.AddChild(addressRow);

        _addressEdit = new LineEdit
        {
            Text = "127.0.0.1",
            PlaceholderText = "Server Address",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        addressRow.AddChild(_addressEdit);

        _portSpinBox = new SpinBox
        {
            MinValue = 1024,
            MaxValue = 65535,
            Step = 1,
            Value = NetworkManager.DefaultPort,
            CustomMinimumSize = new Vector2(100f, 0f)
        };
        addressRow.AddChild(_portSpinBox);

        var hostButton = new Button { Text = "Host Match (Server + Player)" };
        hostButton.Pressed += OnHostPressed;
        _connectionBox.AddChild(hostButton);

        var joinButton = new Button { Text = "Join Match (Client)" };
        joinButton.Pressed += OnJoinPressed;
        _connectionBox.AddChild(joinButton);

        var dedicatedButton = new Button { Text = "Start Dedicated Server" };
        dedicatedButton.Pressed += OnDedicatedServerPressed;
        _connectionBox.AddChild(dedicatedButton);

        // Active session container
        _sessionBox = new VBoxContainer
        {
            Visible = false
        };
        _sessionBox.AddThemeConstantOverride("separation", 6);
        rootVBox.AddChild(_sessionBox);

        _sessionSummaryLabel = new Label();
        _sessionBox.AddChild(_sessionSummaryLabel);

        _playersListLabel = new Label();
        _sessionBox.AddChild(_playersListLabel);

        var controlsHint = new Label
        {
            Text = "Controls: WASD or Arrow Keys to Move",
            Modulate = new Color(0.75f, 0.85f, 1.0f)
        };
        _sessionBox.AddChild(controlsHint);

        var disconnectButton = new Button { Text = "Disconnect" };
        disconnectButton.Pressed += OnDisconnectPressed;
        _sessionBox.AddChild(disconnectButton);

        RefreshUiState();
    }

    public override void _Process(double delta)
    {
        if (_networkManager == null || _matchController == null || _gameWorld == null)
        {
            return;
        }

        if (_networkManager.IsOnline || _networkManager.IsConnecting)
        {
            _hudUpdateAccumulator += delta;
            if (_hudUpdateAccumulator >= 0.1)
            {
                _hudUpdateAccumulator = 0.0;
                UpdateSessionDetails();
            }
        }
    }

    private void OnHostPressed()
    {
        int port = (int)_portSpinBox.Value;
        Error err = _networkManager.StartHost(port, _nameEdit.Text);
        if (err != Error.Ok)
        {
            _statusLabel.Text = $"Failed to start host: {err}";
        }
        RefreshUiState();
    }

    private void OnDedicatedServerPressed()
    {
        int port = (int)_portSpinBox.Value;
        Error err = _networkManager.StartDedicatedServer(port);
        if (err != Error.Ok)
        {
            _statusLabel.Text = $"Failed to start dedicated server: {err}";
        }
        RefreshUiState();
    }

    private void OnJoinPressed()
    {
        int port = (int)_portSpinBox.Value;
        string address = string.IsNullOrWhiteSpace(_addressEdit.Text) ? "127.0.0.1" : _addressEdit.Text.Trim();
        Error err = _networkManager.JoinServer(address, port, _nameEdit.Text);
        if (err != Error.Ok)
        {
            _statusLabel.Text = $"Failed to connect: {err}";
        }
        else
        {
            _statusLabel.Text = $"Connecting to {address}:{port}...";
        }
        RefreshUiState();
    }

    private void OnDisconnectPressed()
    {
        _networkManager.ShutdownSession("User disconnected");
        RefreshUiState();
    }

    private void OnSessionStateChanged() => RefreshUiState();

    private void OnSessionEnded(string reason)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = $"Status: Offline ({reason})";
        }
        RefreshUiState();
    }

    private void OnPlayerListChanged(PlayerIdentity _) => RefreshUiState();
    private void OnPlayerUnregistered(long _) => RefreshUiState();
    private void OnMatchPhaseChanged(MatchPhase _) => RefreshUiState();

    private void RefreshUiState()
    {
        if (_connectionBox == null || _sessionBox == null || _networkManager == null)
        {
            return;
        }

        bool activeOrConnecting = _networkManager.IsOnline || _networkManager.IsConnecting;
        _connectionBox.Visible = !activeOrConnecting;
        _sessionBox.Visible = activeOrConnecting;

        if (_networkManager.IsOnline)
        {
            string role = Multiplayer.IsServer()
                ? (_networkManager.IsDedicatedServer ? "DEDICATED SERVER" : "HOST (Authoritative Server)")
                : "CLIENT";
            _statusLabel.Text = $"Status: Connected [{role}] (Peer {_networkManager.LocalPeerId})";
            UpdateSessionDetails();
        }
    }

    private void UpdateSessionDetails()
    {
        _sessionSummaryLabel.Text =
            $"Match Phase: {_matchController.CurrentPhase} | Time: {_matchController.ElapsedMatchTime:F1}s\n" +
            $"Registered Players: {_networkManager.Players.Count}";

        _hudStringBuilder.Clear();
        foreach (var kvp in _networkManager.Players)
        {
            PlayerIdentity identity = kvp.Value;
            PlayerCharacter? character = _gameWorld.GetPlayerCharacter(identity.PeerId);
            string posText = character != null
                ? $"({character.GlobalPosition.X:F0}, {character.GlobalPosition.Y:F0}) [Auth:{character.GetMultiplayerAuthority()}]"
                : "(Spawning...)";
            _hudStringBuilder.AppendLine($"• [{identity.PeerId}] {identity.DisplayName} (Team {identity.TeamId}) {posText}");
        }

        _playersListLabel.Text = _hudStringBuilder.ToString().TrimEnd();
    }
}
