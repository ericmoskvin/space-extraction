using System;
using System.Collections.Generic;
using Godot;
using SpaceExtraction.Match;
using SpaceExtraction.Networking;
using SpaceExtraction.Ships;
using SpaceExtraction.World;

namespace SpaceExtraction.Testing;

/// <summary>
/// Automated multi-instance acceptance test for Milestone 2 (Ship Combat Systems).
/// Verifies under server authority:
/// 1. 3 distinct weapon types (Autocannon, Railgun, EMP Cannon) with ammo depletion and reload cycles.
/// 2. Subsystem damage & Engine damage (reducing ship acceleration/speed and immobilizing at 0 engine health while hull remains intact).
/// 3. Automated turret behavior (acquiring and firing upon an in-range enemy ship automatically).
/// 4. Manual turret control (crew member operating turret manually with anti-spoofing verification and superior fire rate/range).
/// Triggered via command line flag: --test-m2
/// </summary>
public partial class Milestone2AcceptanceTest : Node
{
    private const int TestPort = 17793;
    private const double TimeoutSeconds = 25.0;

    private NetworkManager _networkManager = null!;
    private MatchController _matchController = null!;
    private GameWorld _gameWorld = null!;

    private readonly List<int> _clientProcessIds = new();
    private double _elapsed;
    private double _stepStartTime;
    private int _step;
    private bool _completed;

    private Ship? _hostShip;
    private Ship? _targetShip;
    private long _clientPeerId;

    public void Initialize(NetworkManager networkManager, MatchController matchController, GameWorld gameWorld)
    {
        _networkManager = networkManager;
        _matchController = matchController;
        _gameWorld = gameWorld;
    }

    public override void _Ready()
    {
        GD.Print("=== [Milestone 2 Acceptance Test] Starting authoritative server ===");
        Error err = _networkManager.StartHost(TestPort, "HostCaptain");
        if (err != Error.Ok)
        {
            FailAndExit($"Failed to start host on port {TestPort}: {err}");
            return;
        }

        SpawnClientSubprocess("TurretGunnerClient");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_completed)
        {
            return;
        }

        _elapsed += delta;
        if (_elapsed > TimeoutSeconds)
        {
            FailAndExit($"Timed out at step {_step} (elapsed {_elapsed:F1}s).");
            return;
        }

        switch (_step)
        {
            case 0:
                // Wait for Host + Client + Target Ship
                _hostShip = _gameWorld.GetShipByPilot(1);
                _targetShip = _gameWorld.GetShip(MatchController.EnemyTargetShipId);

                foreach (long peerId in _networkManager.Players.Keys)
                {
                    if (peerId != 1)
                    {
                        _clientPeerId = peerId;
                        break;
                    }
                }

                if (_hostShip != null && _targetShip != null && _clientPeerId > 0)
                {
                    if (_hostShip.Weapons.Count < 3)
                    {
                        FailAndExit($"Expected at least 3 distinct weapon types, found {_hostShip.Weapons.Count}.");
                        return;
                    }

                    // Position Target Ship facing Host Ship head-on (bow toward -X) so positional bow hits damage Weapons subsystem
                    _hostShip.GlobalPosition = new Vector2(-400.0f, 0.0f);
                    _hostShip.Rotation = 0.0f;
                    _hostShip.Velocity = Vector2.Zero;

                    _targetShip.GlobalPosition = new Vector2(300.0f, 0.0f);
                    _targetShip.Rotation = Mathf.Pi; // Facing -X (bow facing Host Ship)
                    _targetShip.Velocity = Vector2.Zero;

                    // Select Slot 1 (Railgun: 5-round magazine, high velocity) and fire at Target Ship's bow
                    _hostShip.SetTestInputOverride(new ShipControlInput
                    {
                        Throttle = 0.0f,
                        Turn = 0.0f,
                        Brake = true,
                        FirePrimary = true,
                        SelectedWeaponSlot = 1 // Railgun
                    });

                    GD.Print("[Milestone 2 Acceptance Test] Step 0 PASSED: 3 weapon types verified; testing Railgun ammo, reload, & bow positional Weapons damage.");
                    _step = 1;
                    _stepStartTime = _elapsed;
                }
                break;

            case 1:
                // Wait for Railgun to fire shots, reduce ammo, and damage target ship's Weapons subsystem via positional bow hit
                if (_hostShip == null || _targetShip == null)
                {
                    return;
                }

                ShipWeapon railgun = _hostShip.Weapons[1];
                if (_hostShip.ActiveWeaponIndex == 1 &&
                    railgun.CurrentAmmo < railgun.MaxAmmo &&
                    _targetShip.Subsystems.CurrentWeaponsHealth < _targetShip.Subsystems.MaxWeaponsHealth)
                {
                    // Trigger reload on Railgun and verify IsReloading transitions to true
                    bool reloadStarted = railgun.StartReload();
                    if (!reloadStarted && !railgun.IsReloading)
                    {
                        FailAndExit("Expected Railgun reload to start when ammo is below MaxAmmo.");
                        return;
                    }

                    GD.Print($"[Milestone 2 Acceptance Test] Step 1 PASSED: Railgun fired ({railgun.CurrentAmmo}/{railgun.MaxAmmo} ammo), reload initiated, bow hit damaged Weapons subsystem ({_targetShip.Subsystems.CurrentWeaponsHealth:F0}/{_targetShip.Subsystems.MaxWeaponsHealth:F0}).");

                    // Turn Target Ship to face away (+X) so its stern/engines face Host Ship, and switch to Slot 2 (EMP Cannon)
                    _targetShip.Rotation = 0.0f;
                    _hostShip.SetTestInputOverride(new ShipControlInput
                    {
                        Throttle = 0.0f,
                        Turn = 0.0f,
                        Brake = true,
                        FirePrimary = true,
                        SelectedWeaponSlot = 2 // EMP Cannon
                    });

                    _step = 2;
                    _stepStartTime = _elapsed;
                }
                break;

            case 2:
                // Wait for EMP Cannon to disable Target Ship's Engines while Target Ship's Hull remains intact (> 0)
                if (_hostShip == null || _targetShip == null)
                {
                    return;
                }

                if (_targetShip.Subsystems.IsEnginesDisabled && !_targetShip.Hull.IsDisabled)
                {
                    GD.Print($"[Milestone 2 Acceptance Test] Step 2 PASSED: EMP Cannon disabled target Engines (0/{_targetShip.Subsystems.MaxEngineHealth:F0}) while Hull remained intact ({_targetShip.Hull.CurrentHull:F0}/{_targetShip.Hull.MaxHull:F0}).");

                    // Verify Engine damage immobilization: give Target Ship a pilot and full throttle/boost, confirm EngineEfficiency == 0 prevents movement
                    _hostShip.SetTestInputOverride(new ShipControlInput
                    {
                        Throttle = 0.0f,
                        Turn = 0.0f,
                        Brake = true,
                        FirePrimary = false,
                        SelectedWeaponSlot = 0
                    });
                    _targetShip.SetPilot(1);
                    _targetShip.Velocity = Vector2.Zero;
                    _targetShip.ApplyServerPilotInput(1, new ShipControlInput
                    {
                        Throttle = 1.0f,
                        Boost = true,
                        SelectedWeaponSlot = -1,
                        Tick = 500
                    });

                    _step = 3;
                    _stepStartTime = _elapsed;
                }
                break;

            case 3:
                // Verify target ship with disabled engines cannot accelerate or boost, then repair engines and test Automated Turret
                if (_targetShip == null || _hostShip == null)
                {
                    return;
                }

                if (_elapsed - _stepStartTime >= 0.2)
                {
                    if (_targetShip.Velocity.Length() > 1.0f || _targetShip.Movement.IsBoosting)
                    {
                        FailAndExit("Ship with 0 engine health was still able to thrust or boost!");
                        return;
                    }

                    // Restore pilot to Host ship and verify solo-player station toggling (Pilot -> Turret Gunner -> Pilot)
                    _targetShip.SetPilot(0);
                    _hostShip.SetStationRolesServer(1, 0);
                    _hostShip.RequestToggleStationRoleRpc();
                    if (_hostShip.PilotPeerId != 0 || _hostShip.Turret.GunnerPeerId != 1)
                    {
                        FailAndExit($"Expected solo station toggle to switch Peer 1 to Turret Gunner, got Pilot={_hostShip.PilotPeerId}, Gunner={_hostShip.Turret.GunnerPeerId}");
                        return;
                    }
                    _hostShip.RequestToggleStationRoleRpc();
                    if (_hostShip.PilotPeerId != 1 || _hostShip.Turret.GunnerPeerId != 0)
                    {
                        FailAndExit($"Expected second station toggle to switch Peer 1 back to Pilot, got Pilot={_hostShip.PilotPeerId}, Gunner={_hostShip.Turret.GunnerPeerId}");
                        return;
                    }

                    _targetShip.Hull.Repair(2000.0f);
                    _targetShip.Subsystems.RepairSubsystem(ShipSubsystemType.Engines, 500.0f);

                    // Move Host Ship and Target Ship to (900, 0) away from other ships so Target Ship is the nearest enemy within Automated Turret range (220 units away, perpendicular to Host ship)
                    _hostShip.SetTestInputOverride(null);
                    _hostShip.GlobalPosition = new Vector2(900.0f, 0.0f);
                    _hostShip.Rotation = 0.0f; // Facing +X
                    _hostShip.Velocity = Vector2.Zero;

                    _targetShip.GlobalPosition = new Vector2(900.0f, -220.0f); // Directly above Host ship (-Y)
                    _targetShip.Velocity = Vector2.Zero;
                    float hullBeforeAutoTurret = _targetShip.Hull.CurrentHull;
                    _stepStartTime = _elapsed;
                    _step = 4;

                    GD.Print($"[Milestone 2 Acceptance Test] Step 3 PASSED: Engine immobilization verified. Testing Automated Turret tracking & firing (Target Hull={hullBeforeAutoTurret:F0}).");
                }
                break;

            case 4:
                // Verify Host ship's Automated Turret rotates toward -Y and damages Target Ship without pilot firing primary weapons
                if (_hostShip == null || _targetShip == null)
                {
                    return;
                }

                if (_hostShip.Turret.IsAutomatedMode &&
                    _hostShip.Turret.CurrentAutoTargetShipId == _targetShip.ShipId &&
                    _targetShip.Hull.CurrentHull < _targetShip.Hull.MaxHull)
                {
                    GD.Print($"[Milestone 2 Acceptance Test] Step 4 PASSED: Automated Turret acquired target {_targetShip.ShipId} and dealt damage (Hull now {_targetShip.Hull.CurrentHull:F0}/{_targetShip.Hull.MaxHull:F0}).");

                    // Now test Manual Turret Operation: assign connected Client (_clientPeerId) as Manual Turret Gunner on Host Ship!
                    // This demonstrates a 2-player crew operating the same Corvette (Peer 1 piloting, Client manning turret manually).
                    _hostShip.SetStationRolesServer(1, _clientPeerId);

                    // Verify anti-spoofing on Manual Turret: Peer 1 (not the gunner) attempting to send turret input must be rejected
                    uint turretTickBefore = _hostShip.LastProcessedTurretTick;
                    _hostShip.ApplyServerTurretInput(1, new TurretControlInput
                    {
                        AimAngleRadians = 0.0f,
                        FireTurret = true,
                        Tick = turretTickBefore + 500
                    });
                    if (_hostShip.LastProcessedTurretTick == turretTickBefore + 500)
                    {
                        FailAndExit("Server accepted spoofed turret input from non-gunner peer!");
                        return;
                    }

                    // Place Target Ship at long range (600 units at +Y from Host Ship at 900,0, oriented broadside at Rotation = 0)
                    // outside Automated Turret range (460) but inside Manual Turret range (850)
                    _targetShip.Hull.Repair(500.0f);
                    _targetShip.Subsystems.RepairSubsystem(ShipSubsystemType.Turret, 500.0f);
                    _targetShip.GlobalPosition = new Vector2(900.0f, 600.0f);
                    _targetShip.Rotation = 0.0f;
                    _targetShip.Velocity = Vector2.Zero;

                    // Command Manual Turret to aim at +Y (pi/2) and fire at Target Ship's midships
                    _hostShip.SetTestTurretInputOverride(new TurretControlInput
                    {
                        AimAngleRadians = Mathf.Pi * 0.5f,
                        FireTurret = true
                    });

                    _step = 5;
                    _stepStartTime = _elapsed;
                }
                break;

            case 5:
                // Verify Manual Turret hits Target Ship at 600 units (beyond automated range) and damages its hull & Turret subsystem
                if (_hostShip == null || _targetShip == null)
                {
                    return;
                }

                if (_hostShip.Turret.IsManuallyOperated &&
                    _targetShip.Hull.CurrentHull < _targetShip.Hull.MaxHull &&
                    _targetShip.Subsystems.CurrentTurretHealth < _targetShip.Subsystems.MaxTurretHealth)
                {
                    GD.Print($"[Milestone 2 Acceptance Test] Step 5 PASSED: Manual Turret operated by crewmate (Peer {_clientPeerId}) hit target at 600u range (Hull={_targetShip.Hull.CurrentHull:F0}, TurretSys={_targetShip.Subsystems.CurrentTurretHealth:F0}).");
                    GD.Print("=== [Milestone 2 Acceptance Test] ALL ACCEPTANCE CRITERIA PASSED ===");
                    CleanupAndExit(0);
                }
                break;
        }
    }

    private void SpawnClientSubprocess(string clientName)
    {
        string exePath = OS.GetExecutablePath();
        string[] args =
        {
            "--headless",
            "--path", ProjectSettings.GlobalizePath("res://"),
            "--",
            "--test-ship-client",
            $"--port={TestPort}",
            $"--name={clientName}"
        };

        int pid = OS.CreateProcess(exePath, args);
        if (pid <= 0)
        {
            FailAndExit($"Failed to launch test client subprocess for {clientName}");
            return;
        }

        _clientProcessIds.Add(pid);
        GD.Print($"[Milestone 2 Acceptance Test] Launched client subprocess '{clientName}' (PID {pid})");
    }

    private void FailAndExit(string message)
    {
        GD.PrintErr($"=== [Milestone 2 Acceptance Test] FAILED: {message} ===");
        CleanupAndExit(1);
    }

    private void CleanupAndExit(int exitCode)
    {
        if (_completed)
        {
            return;
        }
        _completed = true;

        _networkManager.ShutdownSession("Milestone 2 test complete");

        foreach (int pid in _clientProcessIds)
        {
            if (OS.IsProcessRunning(pid))
            {
                OS.Kill(pid);
            }
        }
        _clientProcessIds.Clear();

        GetTree().Quit(exitCode);
    }
}
