using System;
using Godot;
using SpaceExtraction.Characters;
using SpaceExtraction.Interiors;
using SpaceExtraction.Match;
using SpaceExtraction.Networking;
using SpaceExtraction.Ships;
using SpaceExtraction.World;

namespace SpaceExtraction.UI;

/// <summary>
/// Debug and connection HUD for Milestone 0 & Milestone 1.
/// Displays connection controls, authoritative match/player state, and active ship flight/combat telemetry.
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
    private Label _localShipLabel = null!;
    private Label _playersListLabel = null!;

    private PanelContainer _serverConfigPanel = null!;
    private SpinBox _cfgHullSpin = null!;
    private SpinBox _cfgSubsystemSpin = null!;
    private SpinBox _cfgTurretRangeSpin = null!;
    private SpinBox _cfgWeaponDmgSpin = null!;
    private SpinBox _cfgSpeedSpin = null!;
    private SpinBox _cfgSuppliesSpin = null!;
    private SpinBox _cfgFireHazardSpin = null!;
    private CheckBox _cfgDroneTurretCheck = null!;
    private CheckBox _cfgSpawnDroneCheck = null!;

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
            CustomMinimumSize = new Vector2(390f, 0f)
        };
        AddChild(_panel);

        var rootVBox = new VBoxContainer();
        rootVBox.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(rootVBox);

        var titleLabel = new Label
        {
            Text = "2D SPACE EXTRACTION — MILESTONE 4",
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

        var configToggleBtn = new Button { Text = "Server Balance & Rules Settings..." };
        configToggleBtn.Pressed += () =>
        {
            if (_serverConfigPanel != null)
            {
                _serverConfigPanel.Visible = !_serverConfigPanel.Visible;
            }
        };
        _connectionBox.AddChild(configToggleBtn);

        // Active session container
        _sessionBox = new VBoxContainer
        {
            Visible = false
        };
        _sessionBox.AddThemeConstantOverride("separation", 6);
        rootVBox.AddChild(_sessionBox);

        _sessionSummaryLabel = new Label();
        _sessionBox.AddChild(_sessionSummaryLabel);

        _localShipLabel = new Label
        {
            Modulate = new Color(0.65f, 0.95f, 0.8f)
        };
        _sessionBox.AddChild(_localShipLabel);

        _playersListLabel = new Label();
        _sessionBox.AddChild(_playersListLabel);

        var legendHeader = new Label
        {
            Text = "SHIP ROOMS & SYSTEMS LEGEND:",
            Modulate = new Color(0.85f, 0.88f, 0.95f)
        };
        _sessionBox.AddChild(legendHeader);

        static HBoxContainer CreateLegendRow(Color swatchColor, string text)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            var rect = new ColorRect
            {
                Color = swatchColor,
                CustomMinimumSize = new Vector2(14f, 14f)
            };
            row.AddChild(rect);
            var label = new Label
            {
                Text = text,
                Modulate = new Color(0.85f, 0.9f, 0.95f)
            };
            row.AddChild(label);
            return row;
        }

        _sessionBox.AddChild(CreateLegendRow(new Color(0.20f, 0.85f, 1.0f), "Cockpit / Pilot & Weapons (Bow)"));
        _sessionBox.AddChild(CreateLegendRow(new Color(1.0f, 0.65f, 0.20f), "Central Hub / Dorsal Turret (Midships)"));
        _sessionBox.AddChild(CreateLegendRow(new Color(1.0f, 0.45f, 0.20f), "Engine Room / Engineering (Stern)"));
        _sessionBox.AddChild(CreateLegendRow(new Color(0.95f, 0.85f, 0.25f), "Cargo Bay / Cargo Console (Port)"));
        _sessionBox.AddChild(CreateLegendRow(new Color(0.25f, 0.95f, 0.55f), "Medbay / Medical Console (Starboard)"));

        var doorRow = new HBoxContainer();
        doorRow.AddThemeConstantOverride("separation", 6);
        var openSwatch = new ColorRect { Color = new Color(0.25f, 0.95f, 0.45f), CustomMinimumSize = new Vector2(14f, 14f) };
        var closedSwatch = new ColorRect { Color = new Color(0.95f, 0.30f, 0.20f), CustomMinimumSize = new Vector2(14f, 14f) };
        doorRow.AddChild(openSwatch);
        doorRow.AddChild(closedSwatch);
        doorRow.AddChild(new Label { Text = "Bulkhead Doors: Green = Open, Red = Closed [F]", Modulate = new Color(0.85f, 0.9f, 0.95f) });
        _sessionBox.AddChild(doorRow);

        var controlsHint = new Label
        {
            Text = "WASD: Fly/Walk | E: Use/Leave/Extinguish | R: Repair/Extinguish/Reload | F: Door | Space/Click: Fire | 1/2/3: Wpn | T: Swap Role | Scroll: Zoom",
            Modulate = new Color(0.75f, 0.85f, 1.0f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _sessionBox.AddChild(controlsHint);

        var disconnectButton = new Button { Text = "Disconnect" };
        disconnectButton.Pressed += OnDisconnectPressed;
        _sessionBox.AddChild(disconnectButton);

        BuildServerConfigPanel();
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

    private void BuildServerConfigPanel()
    {
        _serverConfigPanel = new PanelContainer
        {
            Position = new Vector2(420f, 16f),
            CustomMinimumSize = new Vector2(430f, 0f),
            Visible = false
        };
        AddChild(_serverConfigPanel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        _serverConfigPanel.AddChild(vbox);

        var title = new Label
        {
            Text = "SERVER COMBAT & BALANCE CONFIG",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.4f));
        vbox.AddChild(title);

        static HBoxContainer CreateConfigRow(string labelText, Control inputControl)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var lbl = new Label
            {
                Text = labelText,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            row.AddChild(lbl);
            row.AddChild(inputControl);
            return row;
        }

        _cfgHullSpin = new SpinBox { MinValue = 100, MaxValue = 5000, Step = 50, Value = ServerConfig.DefaultCorvetteHullHp, CustomMinimumSize = new Vector2(110f, 0f) };
        vbox.AddChild(CreateConfigRow("Corvette Hull HP (Default 720):", _cfgHullSpin));

        _cfgSubsystemSpin = new SpinBox { MinValue = 0.25, MaxValue = 5.0, Step = 0.25, Value = ServerConfig.DefaultSubsystemHealthMultiplier, CustomMinimumSize = new Vector2(110f, 0f) };
        vbox.AddChild(CreateConfigRow("Subsystem HP Multiplier:", _cfgSubsystemSpin));

        _cfgTurretRangeSpin = new SpinBox { MinValue = 300, MaxValue = 2500, Step = 50, Value = ServerConfig.DefaultTurretAutoRange, CustomMinimumSize = new Vector2(110f, 0f) };
        vbox.AddChild(CreateConfigRow("Turret Auto Range (Default 1100):", _cfgTurretRangeSpin));

        _cfgWeaponDmgSpin = new SpinBox { MinValue = 0.25, MaxValue = 5.0, Step = 0.25, Value = ServerConfig.DefaultWeaponDamageMultiplier, CustomMinimumSize = new Vector2(110f, 0f) };
        vbox.AddChild(CreateConfigRow("Weapon Damage Multiplier:", _cfgWeaponDmgSpin));

        _cfgSpeedSpin = new SpinBox { MinValue = 0.25, MaxValue = 3.0, Step = 0.1, Value = ServerConfig.DefaultShipSpeedMultiplier, CustomMinimumSize = new Vector2(110f, 0f) };
        vbox.AddChild(CreateConfigRow("Ship Speed Multiplier:", _cfgSpeedSpin));

        _cfgSuppliesSpin = new SpinBox { MinValue = 0, MaxValue = 500, Step = 10, Value = ServerConfig.DefaultStartingRepairSupplies, CustomMinimumSize = new Vector2(110f, 0f) };
        vbox.AddChild(CreateConfigRow("Starting Repair Supplies:", _cfgSuppliesSpin));

        _cfgFireHazardSpin = new SpinBox { MinValue = 0.0, MaxValue = 3.0, Step = 0.25, Value = ServerConfig.DefaultFireHazardMultiplier, CustomMinimumSize = new Vector2(110f, 0f) };
        vbox.AddChild(CreateConfigRow("Fire Hazard Multiplier:", _cfgFireHazardSpin));

        _cfgDroneTurretCheck = new CheckBox { Text = "Target Drone Turret Active (Fires Back)", ButtonPressed = false };
        vbox.AddChild(_cfgDroneTurretCheck);

        _cfgSpawnDroneCheck = new CheckBox { Text = "Spawn Target Drone on Start", ButtonPressed = true };
        vbox.AddChild(_cfgSpawnDroneCheck);

        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 6);
        vbox.AddChild(btnRow);

        var resetBtn = new Button { Text = "Reset Defaults", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        resetBtn.Pressed += ResetConfigInputsToDefaults;
        btnRow.AddChild(resetBtn);

        var closeBtn = new Button { Text = "Close", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        closeBtn.Pressed += () => _serverConfigPanel.Visible = false;
        btnRow.AddChild(closeBtn);

        var hostWithSettingsBtn = new Button { Text = "Host Match with These Settings" };
        hostWithSettingsBtn.Pressed += () =>
        {
            ApplyUiConfigToServer();
            _serverConfigPanel.Visible = false;
            OnHostPressed();
        };
        vbox.AddChild(hostWithSettingsBtn);
    }

    private void ApplyUiConfigToServer()
    {
        if (_matchController == null || _cfgHullSpin == null)
        {
            return;
        }

        var cfg = new ServerConfig
        {
            CorvetteHullHp = (float)_cfgHullSpin.Value,
            SubsystemHealthMultiplier = (float)_cfgSubsystemSpin.Value,
            TurretAutoRange = (float)_cfgTurretRangeSpin.Value,
            WeaponDamageMultiplier = (float)_cfgWeaponDmgSpin.Value,
            ShipSpeedMultiplier = (float)_cfgSpeedSpin.Value,
            StartingRepairSupplies = (float)_cfgSuppliesSpin.Value,
            FireHazardMultiplier = (float)_cfgFireHazardSpin.Value,
            TargetDroneTurretActive = _cfgDroneTurretCheck.ButtonPressed,
            SpawnTargetDrone = _cfgSpawnDroneCheck.ButtonPressed
        };

        _matchController.SetServerConfig(cfg);
    }

    private void ResetConfigInputsToDefaults()
    {
        if (_cfgHullSpin == null)
        {
            return;
        }

        _cfgHullSpin.Value = ServerConfig.DefaultCorvetteHullHp;
        _cfgSubsystemSpin.Value = ServerConfig.DefaultSubsystemHealthMultiplier;
        _cfgTurretRangeSpin.Value = ServerConfig.DefaultTurretAutoRange;
        _cfgWeaponDmgSpin.Value = ServerConfig.DefaultWeaponDamageMultiplier;
        _cfgSpeedSpin.Value = ServerConfig.DefaultShipSpeedMultiplier;
        _cfgSuppliesSpin.Value = ServerConfig.DefaultStartingRepairSupplies;
        _cfgFireHazardSpin.Value = ServerConfig.DefaultFireHazardMultiplier;
        _cfgDroneTurretCheck.ButtonPressed = false;
        _cfgSpawnDroneCheck.ButtonPressed = true;
    }

    private void OnHostPressed()
    {
        ApplyUiConfigToServer();
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
        ApplyUiConfigToServer();
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
        if (activeOrConnecting && _serverConfigPanel != null)
        {
            _serverConfigPanel.Visible = false;
        }

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
        var ships = _gameWorld.GetAllShips();
        var cfg = _matchController.Config;
        _sessionSummaryLabel.Text =
            $"Match Phase: {_matchController.CurrentPhase} | Time: {_matchController.ElapsedMatchTime:F1}s\n" +
            $"Players: {_networkManager.Players.Count} | Active Ships: {ships.Count}\n" +
            $"Rules: Hull={cfg.CorvetteHullHp:F0} HP | TurretRange={cfg.TurretAutoRange:F0} | WpnDmg={cfg.WeaponDamageMultiplier:F2}x | Spd={cfg.ShipSpeedMultiplier:F2}x | Supplies={cfg.StartingRepairSupplies:F0} | Fire={cfg.FireHazardMultiplier:F2}x";

        PlayerCharacter? localChar = _gameWorld.GetPlayerCharacter(_networkManager.LocalPeerId);
        Ship? localShip = (localChar != null && localChar.LocationContext.OccupiedShipId > 0)
            ? _gameWorld.GetShip(localChar.LocationContext.OccupiedShipId)
            : (_gameWorld.GetShipByPilot(_networkManager.LocalPeerId) ?? _gameWorld.GetShip((int)_networkManager.LocalPeerId));

        if (localShip != null)
        {
            string boostState = localShip.Movement.IsBoosting
                ? "BOOSTING"
                : (localShip.Movement.BoostCooldownRemaining > 0f
                    ? $"CD {localShip.Movement.BoostCooldownRemaining:F1}s"
                    : "READY");
            string statusState = localShip.Hull.IsDisabled ? "DISABLED" : "OPERATIONAL";
            ShipWeapon wpn = localShip.PrimaryWeapon;
            string ammoState = wpn.IsReloading
                ? $"RELOADING ({wpn.ReloadTimeRemaining:F1}s)"
                : $"{wpn.CurrentAmmo}/{wpn.MaxAmmo}";
            string turretState = localShip.Turret.IsManuallyOperated
                ? $"MANUAL (Peer {localShip.Turret.GunnerPeerId})"
                : "AUTO";

            string interiorLine = string.Empty;
            if (localChar != null && localChar.LocationContext.Mode == PlayerLocationMode.ShipInterior)
            {
                string role;
                string prompt = string.Empty;
                if (localChar.LocationContext.IsPiloting)
                {
                    role = "PILOTING";
                    prompt = " | [E] Leave Station";
                }
                else if (localChar.LocationContext.IsOperatingTurret)
                {
                    role = "TURRET GUNNER";
                    prompt = " | [E] Leave Station";
                }
                else
                {
                    role = "ON FOOT";
                    var nearbyConsole = localShip.Interior.FindNearestInteractableConsole(localChar.ShipLocalPosition);
                    if (nearbyConsole != null)
                    {
                        prompt = $" | [E] Use {nearbyConsole.ConsoleName}";
                    }
                    else
                    {
                        var nearbyDoor = localShip.Interior.FindNearestInteractableDoor(localChar.ShipLocalPosition);
                        if (nearbyDoor != null)
                        {
                            prompt = $" | [F] {(nearbyDoor.IsOpen ? "Close" : "Open")} {nearbyDoor.DoorName}";
                        }
                    }
                }
                interiorLine = $"\nStatus: {role} in {localChar.CurrentInteriorRoom}{prompt}";
            }

            string emergencyLine = string.Empty;
            if (localShip.DamageControl != null)
            {
                if (localShip.DamageControl.IsAnyRoomOnFire)
                {
                    var fireRooms = new System.Collections.Generic.List<string>();
                    for (int r = 0; r < localShip.Interior.Rooms.Count; r++)
                    {
                        InteriorRoomType room = localShip.Interior.Rooms[r].RoomType;
                        if (localShip.DamageControl.IsRoomOnFire(room))
                        {
                            fireRooms.Add($"{room} ({localShip.DamageControl.GetRoomFireIntensity(room):F0}%)");
                        }
                    }
                    emergencyLine = $"\nEMERGENCY: {string.Join(", ", fireRooms)} ON FIRE — Press [E]/[R] in room to Extinguish & Repair!";
                }
                else if (localShip.Subsystems.IsEnginesDisabled || localShip.Subsystems.IsWeaponsDisabled || localShip.Subsystems.IsTurretDisabled)
                {
                    emergencyLine = "\nWARNING: Subsystems Disabled — Press [R] in compartment or use Console to Repair!";
                }
            }

            string suppliesText = localShip.DamageControl != null
                ? $" | Supplies: {localShip.DamageControl.CurrentRepairSupplies:F0}/{localShip.DamageControl.MaxSupplies:F0}"
                : string.Empty;

            _localShipLabel.Text =
                $"Ship: {localShip.ShipName} [{localShip.ShipClass}] ({statusState})\n" +
                $"Hull: {localShip.Hull.CurrentHull:F0}/{localShip.Hull.MaxHull:F0} | Eng: {localShip.Subsystems.CurrentEngineHealth:F0} | Wpn: {localShip.Subsystems.CurrentWeaponsHealth:F0} | Tur: {localShip.Subsystems.CurrentTurretHealth:F0}{suppliesText}\n" +
                $"Weapon [{localShip.ActiveWeaponIndex + 1}]: {wpn.WeaponName} ({ammoState}) | Turret: {turretState}\n" +
                $"Speed: {localShip.Velocity.Length():F0} u/s | Boost: {boostState}" +
                interiorLine +
                emergencyLine;
            _localShipLabel.Visible = true;
        }
        else
        {
            _localShipLabel.Visible = false;
        }

        _hudStringBuilder.Clear();
        _hudStringBuilder.AppendLine("Ships in Sector:");
        for (int i = 0; i < ships.Count; i++)
        {
            Ship ship = ships[i];
            string stateTag = ship.Hull.IsDisabled ? " [DISABLED]" : (ship.Subsystems.IsEnginesDisabled ? " [ENG OUT]" : "");
            if (ship.DamageControl != null && ship.DamageControl.IsAnyRoomOnFire)
            {
                stateTag += $" [FIRE x{ship.DamageControl.BurningRoomCount}]";
            }
            _hudStringBuilder.AppendLine(
                $"• [{ship.ShipId}] {ship.ShipName} Hull:{ship.Hull.CurrentHull:F0}/{ship.Hull.MaxHull:F0} Eng:{ship.Subsystems.CurrentEngineHealth:F0} Wpn:{ship.Subsystems.CurrentWeaponsHealth:F0} Tur:{ship.Subsystems.CurrentTurretHealth:F0}{stateTag}");
        }

        _playersListLabel.Text = _hudStringBuilder.ToString().TrimEnd();
    }
}
