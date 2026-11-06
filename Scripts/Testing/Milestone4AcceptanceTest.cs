using System;
using System.Collections.Generic;
using Godot;
using SpaceExtraction.Characters;
using SpaceExtraction.Interiors;
using SpaceExtraction.Match;
using SpaceExtraction.Networking;
using SpaceExtraction.Ships;
using SpaceExtraction.World;

namespace SpaceExtraction.Testing;

/// <summary>
/// Automated multi-instance acceptance test for Milestone 4 (Damage Control).
/// Verifies under server authority:
/// 1. Combat hit ignites Engine Room fire and disables Engines subsystem.
/// 2. Controlled fire spread across open bulkhead doors, blocked by closed doors.
/// 3. Ongoing fire attrition damage to burning compartments' subsystems.
/// 4. Firefighting mechanics (extinguishing fires).
/// 5. Fire blocking physical repair until fully extinguished.
/// 6. Physical subsystem repair consuming finite repair supplies.
/// Triggered via command line flag: --test-m4
/// </summary>
public partial class Milestone4AcceptanceTest : Node
{
    private const int TestPort = 17795;
    private const double TimeoutSeconds = 30.0;

    private NetworkManager _networkManager = null!;
    private MatchController _matchController = null!;
    private GameWorld _gameWorld = null!;

    private readonly List<int> _clientProcessIds = new();
    private double _elapsed;
    private double _stepStartTime;
    private int _step;
    private int _subStep;
    private bool _completed;

    private Ship? _hostShip;
    private PlayerCharacter? _hostCharacter;
    private long _clientPeerId;

    public void Initialize(NetworkManager networkManager, MatchController matchController, GameWorld gameWorld)
    {
        _networkManager = networkManager;
        _matchController = matchController;
        _gameWorld = gameWorld;
    }

    public override void _Ready()
    {
        GD.Print("=== [Milestone 4 Acceptance Test] Starting authoritative server ===");
        Error err = _networkManager.StartHost(TestPort, "HostCaptain");
        if (err != Error.Ok)
        {
            FailAndExit($"Failed to start host on port {TestPort}: {err}");
            return;
        }

        SpawnClientSubprocess("DamageControlCrewClient");
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
            FailAndExit($"Timed out at step {_step} (substep {_subStep}, elapsed {_elapsed:F1}s).");
            return;
        }

        switch (_step)
        {
            case 0:
                // Wait for Host + Client, then inflict heavy stern engine damage to ignite Engine Room fire & disable Engines
                _hostShip = _gameWorld.GetShipByPilot(1);
                _hostCharacter = _gameWorld.GetPlayerCharacter(1);

                foreach (long peerId in _networkManager.Players.Keys)
                {
                    if (peerId != 1)
                    {
                        _clientPeerId = peerId;
                        break;
                    }
                }

                if (_hostShip != null && _hostCharacter != null && _clientPeerId > 0)
                {
                    // Inflict heavy subsystem damage to stern Engines
                    _hostShip.ApplyCombatHitServer(30.0f, 120.0f, ShipSubsystemType.Engines);

                    if (!_hostShip.Subsystems.IsEnginesDisabled || _hostShip.Subsystems.CurrentEngineHealth > 0.0f)
                    {
                        FailAndExit($"Expected Engines to be disabled (0 HP), found {_hostShip.Subsystems.CurrentEngineHealth}/{_hostShip.Subsystems.MaxEngineHealth}.");
                        return;
                    }

                    if (!_hostShip.DamageControl.IsRoomOnFire(InteriorRoomType.EngineRoom))
                    {
                        FailAndExit("Expected Engine Room to be ignited by heavy combat hit.");
                        return;
                    }

                    GD.Print($"[Milestone 4 Acceptance Test] Step 0 PASSED: Heavy combat hit disabled Engines (0/{_hostShip.Subsystems.MaxEngineHealth:F0} HP) and ignited Engine Room fire (Intensity={_hostShip.DamageControl.GetRoomFireIntensity(InteriorRoomType.EngineRoom):F1}%).");
                    _step = 1;
                    _stepStartTime = _elapsed;
                    _subStep = 0;
                }
                break;

            case 1:
                // Test controlled fire spread across open doors & containment by closed doors
                if (_hostShip == null || _hostCharacter == null)
                {
                    return;
                }

                if (_subStep == 0)
                {
                    // Close Bridge Door (0), Cargo Door (2), Medbay Door (3); leave Engine Door (1) OPEN
                    _hostShip.SetDoorOpenServer(0, false);
                    _hostShip.SetDoorOpenServer(2, false);
                    _hostShip.SetDoorOpenServer(3, false);
                    _hostShip.SetDoorOpenServer(1, true);

                    _stepStartTime = _elapsed;
                    _subStep = 1;
                }
                else if (_subStep == 1)
                {
                    // Verify that closed doors contain the fire away from Cockpit, Cargo, and Medbay
                    if (_hostShip.DamageControl.IsRoomOnFire(InteriorRoomType.Cockpit) ||
                        _hostShip.DamageControl.IsRoomOnFire(InteriorRoomType.Cargo) ||
                        _hostShip.DamageControl.IsRoomOnFire(InteriorRoomType.Medical))
                    {
                        FailAndExit("Fire breached closed bulkhead doors into isolated compartments!");
                        return;
                    }

                    // Check if fire has spread across the open Engine Door into Central Hub
                    if (_hostShip.DamageControl.IsRoomOnFire(InteriorRoomType.CentralHub))
                    {
                        // Verify that Turret subsystem in Central Hub is taking fire attrition damage
                        if (_hostShip.Subsystems.CurrentTurretHealth >= _hostShip.Subsystems.MaxTurretHealth)
                        {
                            // Wait a short moment for attrition tick
                            return;
                        }

                        GD.Print($"[Milestone 4 Acceptance Test] Step 1 PASSED: Fire spread across open Engine Door into Central Hub ({_hostShip.DamageControl.GetRoomFireIntensity(InteriorRoomType.CentralHub):F1}%); closed doors contained fire; Turret taking fire damage ({_hostShip.Subsystems.CurrentTurretHealth:F1}/{_hostShip.Subsystems.MaxTurretHealth:F0} HP).");
                        _step = 2;
                        _stepStartTime = _elapsed;
                        _subStep = 0;
                    }
                }
                break;

            case 2:
                // Leave Cockpit, open Bridge Door, walk to Central Hub, extinguish Central Hub fire, and proceed to Engine Room
                if (_hostShip == null || _hostCharacter == null)
                {
                    return;
                }

                if (_subStep == 0)
                {
                    // Host leaves Pilot Console
                    _hostShip.HandlePlayerInteriorInteractServer(1);

                    if (_hostShip.PilotPeerId != 0 || !_hostCharacter.LocationContext.IsOnFootInShip)
                    {
                        FailAndExit("Expected player to transition to On-Foot after leaving Pilot Console.");
                        return;
                    }

                    // Open Bridge Door to allow passage into Central Hub
                    _hostShip.SetDoorOpenServer(0, true);

                    // Command character to walk -X toward Central Hub
                    _hostCharacter.SetTestInteriorMoveOverride(new Vector2(-1.0f, 0.0f));
                    _subStep = 1;
                }
                else if (_subStep == 1)
                {
                    if (_hostCharacter.CurrentInteriorRoom == InteriorRoomType.CentralHub)
                    {
                        // Stop moving and extinguish the Central Hub fire
                        _hostCharacter.SetTestInteriorMoveOverride(null);

                        while (_hostShip.DamageControl.IsRoomOnFire(InteriorRoomType.CentralHub))
                        {
                            _hostShip.HandlePlayerInteriorInteractServer(1);
                        }

                        if (_hostShip.DamageControl.IsRoomOnFire(InteriorRoomType.CentralHub))
                        {
                            FailAndExit("Failed to extinguish fire in Central Hub.");
                            return;
                        }

                        // Now walk -X through Engine Door into Engine Room
                        _hostCharacter.SetTestInteriorMoveOverride(new Vector2(-1.0f, 0.0f));
                        _subStep = 2;
                    }
                }
                else if (_subStep == 2)
                {
                    if (_hostCharacter.CurrentInteriorRoom == InteriorRoomType.EngineRoom && _hostCharacter.ShipLocalPosition.X < -104.0f)
                    {
                        _hostCharacter.SetTestInteriorMoveOverride(null);
                        GD.Print("[Milestone 4 Acceptance Test] Step 2 PASSED: Extinguished Central Hub fire and moved to Engine Room.");
                        _step = 3;
                        _stepStartTime = _elapsed;
                        _subStep = 0;
                    }
                }
                break;

            case 3:
                // Verify fire blocks repair, extinguish Engine Room fire, and repair Engines using finite supplies
                if (_hostShip == null || _hostCharacter == null)
                {
                    return;
                }

                if (_subStep == 0)
                {
                    if (!_hostShip.DamageControl.IsRoomOnFire(InteriorRoomType.EngineRoom))
                    {
                        FailAndExit("Expected Engine Room to still be on fire before repair test.");
                        return;
                    }

                    // 1. Verify fire blocks repair!
                    bool repairSucceededWhileBurning = _hostShip.DamageControl.TryRepairRoomSubsystem(
                        InteriorRoomType.EngineRoom,
                        _hostShip.Subsystems,
                        ignoreCooldown: true);

                    if (repairSucceededWhileBurning)
                    {
                        FailAndExit("Expected repair attempt to be blocked while room is on fire!");
                        return;
                    }

                    // 2. Extinguish the Engine Room fire
                    while (_hostShip.DamageControl.IsRoomOnFire(InteriorRoomType.EngineRoom))
                    {
                        _hostShip.HandlePlayerInteriorInteractServer(1);
                    }

                    if (_hostShip.DamageControl.IsRoomOnFire(InteriorRoomType.EngineRoom))
                    {
                        FailAndExit("Failed to extinguish fire in Engine Room.");
                        return;
                    }

                    float suppliesBefore = _hostShip.DamageControl.CurrentRepairSupplies;

                    // 3. Perform repair interaction via Engineering Console
                    _hostShip.HandlePlayerInteriorInteractServer(1);

                    if (_hostShip.Subsystems.IsEnginesDisabled || _hostShip.Subsystems.CurrentEngineHealth <= 0.0f)
                    {
                        FailAndExit($"Expected Engines to be repaired and restored, but health is {_hostShip.Subsystems.CurrentEngineHealth:F1}.");
                        return;
                    }

                    if (_hostShip.DamageControl.CurrentRepairSupplies >= suppliesBefore)
                    {
                        FailAndExit($"Expected repair supplies to be consumed ({suppliesBefore:F1} -> {_hostShip.DamageControl.CurrentRepairSupplies:F1}).");
                        return;
                    }

                    GD.Print($"[Milestone 4 Acceptance Test] Step 3 PASSED: Verified fire blocked repair; extinguished Engine Room fire; repaired Engines (HP: 0 -> {_hostShip.Subsystems.CurrentEngineHealth:F0}, Supplies: {suppliesBefore:F0} -> {_hostShip.DamageControl.CurrentRepairSupplies:F0}).");
                    GD.Print("=== [Milestone 4 Acceptance Test] ALL ACCEPTANCE CRITERIA PASSED ===");
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
        GD.Print($"[Milestone 4 Acceptance Test] Launched client subprocess '{clientName}' (PID {pid})");
    }

    private void FailAndExit(string message)
    {
        GD.PrintErr($"=== [Milestone 4 Acceptance Test] FAILED: {message} ===");
        CleanupAndExit(1);
    }

    private void CleanupAndExit(int exitCode)
    {
        if (_completed)
        {
            return;
        }
        _completed = true;

        _networkManager.ShutdownSession("Milestone 4 test complete");

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
