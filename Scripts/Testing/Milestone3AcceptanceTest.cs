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
/// Automated multi-instance acceptance test for Milestone 3 (Physical Ship Interiors).
/// Verifies under server authority:
/// 1. Authored Corvette interior layout (5 rooms, 4 bulkhead doors, 5 consoles).
/// 2. Leaving pilot controls while ship coasts through space.
/// 3. Closed bulkhead door blocking pedestrian movement.
/// 4. Walking through interior compartments (Central Hub, Cargo Bay, Medical, Engine Room).
/// 5. Operational console interactions (Cargo, Medical, Engineering repair).
/// 6. Returning to Cockpit, mounting Pilot Console, and resuming flight controls.
/// Triggered via command line flag: --test-m3
/// </summary>
public partial class Milestone3AcceptanceTest : Node
{
    private const int TestPort = 17794;
    private const double TimeoutSeconds = 25.0;

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
    private float _engHealthBefore;

    public void Initialize(NetworkManager networkManager, MatchController matchController, GameWorld gameWorld)
    {
        _networkManager = networkManager;
        _matchController = matchController;
        _gameWorld = gameWorld;
    }

    public override void _Ready()
    {
        GD.Print("=== [Milestone 3 Acceptance Test] Starting authoritative server ===");
        Error err = _networkManager.StartHost(TestPort, "HostCaptain");
        if (err != Error.Ok)
        {
            FailAndExit($"Failed to start host on port {TestPort}: {err}");
            return;
        }

        SpawnClientSubprocess("InteriorCrewClient");
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
                // Wait for Host + Client + Ship Interior
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
                    if (_hostShip.Interior.Rooms.Count != 5)
                    {
                        FailAndExit($"Expected 5 interior rooms, found {_hostShip.Interior.Rooms.Count}.");
                        return;
                    }

                    if (_hostShip.Interior.Doors.Count != 4)
                    {
                        FailAndExit($"Expected 4 bulkhead doors, found {_hostShip.Interior.Doors.Count}.");
                        return;
                    }

                    if (_hostShip.Interior.Consoles.Count != 5)
                    {
                        FailAndExit($"Expected 5 consoles, found {_hostShip.Interior.Consoles.Count}.");
                        return;
                    }

                    if (_hostShip.PilotPeerId != 1 || _hostCharacter.CurrentInteriorRoom != InteriorRoomType.Cockpit)
                    {
                        FailAndExit($"Expected Host to start as Pilot in Cockpit, found Pilot={_hostShip.PilotPeerId}, Room={_hostCharacter.CurrentInteriorRoom}.");
                        return;
                    }

                    // Accelerate ship forward to confirm interior tracking works while ship is in motion
                    _hostShip.SetTestInputOverride(new ShipControlInput
                    {
                        Throttle = 1.0f,
                        Turn = 0.0f,
                        Brake = false,
                        Boost = false
                    });

                    if (_hostShip.Velocity.Length() > 60.0f)
                    {
                        GD.Print($"[Milestone 3 Acceptance Test] Step 0 PASSED: Corvette interior verified (5 rooms, 4 doors, 5 consoles); ship accelerating ({_hostShip.Velocity.Length():F0} u/s).");
                        _step = 1;
                        _stepStartTime = _elapsed;
                        _subStep = 0;
                    }
                }
                break;

            case 1:
                // Leave Pilot Console while ship coasts; close Door 0 (Bridge Door) and verify it blocks movement
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

                    // Close Door 0 (Bridge Door at X = 52)
                    _hostShip.SetDoorOpenServer(0, false);
                    _hostShip.SetTestInputOverride(null);

                    // Command character to walk -X into the closed bulkhead
                    _hostCharacter.SetTestInteriorMoveOverride(new Vector2(-1.0f, 0.0f));
                    _stepStartTime = _elapsed;
                    _subStep = 1;
                }
                else if (_subStep == 1)
                {
                    if (_elapsed - _stepStartTime >= 0.35)
                    {
                        if (_hostCharacter.ShipLocalPosition.X <= 52.0f || _hostCharacter.CurrentInteriorRoom != InteriorRoomType.Cockpit)
                        {
                            FailAndExit($"Closed Bridge Door failed to block character! Pos={_hostCharacter.ShipLocalPosition}, Room={_hostCharacter.CurrentInteriorRoom}");
                            return;
                        }

                        GD.Print($"[Milestone 3 Acceptance Test] Step 1 PASSED: On-foot transition verified; closed door successfully blocked movement (X={_hostCharacter.ShipLocalPosition.X:F1} > 52.0).");

                        // Open Bridge Door to allow passage
                        _hostShip.SetDoorOpenServer(0, true);
                        _hostCharacter.SetTestInteriorMoveOverride(null);
                        _step = 2;
                        _stepStartTime = _elapsed;
                        _subStep = 0;
                    }
                }
                break;

            case 2:
                // Walk through open Bridge Door into Central Hub, then into Cargo Bay and interact with Cargo Console
                if (_hostShip == null || _hostCharacter == null)
                {
                    return;
                }

                if (_subStep == 0)
                {
                    // Walk -X toward Central Hub
                    _hostCharacter.SetTestInteriorMoveOverride(new Vector2(-1.0f, 0.0f));
                    _subStep = 1;
                }
                else if (_subStep == 1)
                {
                    if (_hostCharacter.CurrentInteriorRoom == InteriorRoomType.CentralHub && _hostCharacter.ShipLocalPosition.X < 5.0f)
                    {
                        // Now walk -Y toward Cargo Bay
                        _hostCharacter.SetTestInteriorMoveOverride(new Vector2(0.0f, -1.0f));
                        _subStep = 2;
                    }
                }
                else if (_subStep == 2)
                {
                    if (_hostCharacter.CurrentInteriorRoom == InteriorRoomType.Cargo && _hostCharacter.ShipLocalPosition.Y < -76.0f)
                    {
                        _hostCharacter.SetTestInteriorMoveOverride(null);
                        _hostShip.HandlePlayerInteriorInteractServer(1);

                        if (_hostShip.Interior.LastInteractedConsoleType != InteriorConsoleType.Cargo)
                        {
                            FailAndExit($"Expected Cargo Console interaction, got {_hostShip.Interior.LastInteractedConsoleType}.");
                            return;
                        }

                        GD.Print($"[Milestone 3 Acceptance Test] Step 2 PASSED: Walked through Bridge Door into Central Hub and Cargo Bay; interacted with Cargo Console (Count={_hostShip.Interior.ConsoleInteractionCount}).");
                        _step = 3;
                        _stepStartTime = _elapsed;
                        _subStep = 0;
                    }
                }
                break;

            case 3:
                // Walk +Y across Central Hub and Medbay Door into Medical Room and interact with Medical Console
                if (_hostShip == null || _hostCharacter == null)
                {
                    return;
                }

                if (_subStep == 0)
                {
                    _hostCharacter.SetTestInteriorMoveOverride(new Vector2(0.0f, 1.0f));
                    _subStep = 1;
                }
                else if (_subStep == 1)
                {
                    if (_hostCharacter.CurrentInteriorRoom == InteriorRoomType.Medical && _hostCharacter.ShipLocalPosition.Y > 76.0f)
                    {
                        _hostCharacter.SetTestInteriorMoveOverride(null);
                        _hostShip.HandlePlayerInteriorInteractServer(1);

                        if (_hostShip.Interior.LastInteractedConsoleType != InteriorConsoleType.Medical)
                        {
                            FailAndExit($"Expected Medical Console interaction, got {_hostShip.Interior.LastInteractedConsoleType}.");
                            return;
                        }

                        GD.Print($"[Milestone 3 Acceptance Test] Step 3 PASSED: Traversed Central Hub into Medbay; interacted with Medical Console (Count={_hostShip.Interior.ConsoleInteractionCount}).");
                        _step = 4;
                        _stepStartTime = _elapsed;
                        _subStep = 0;
                    }
                }
                break;

            case 4:
                // Damage ship engines slightly, walk to Engine Room, and use Engineering Console to repair engines
                if (_hostShip == null || _hostCharacter == null)
                {
                    return;
                }

                if (_subStep == 0)
                {
                    _hostShip.Subsystems.ApplySubsystemDamage(ShipSubsystemType.Engines, 20.0f);
                    _engHealthBefore = _hostShip.Subsystems.CurrentEngineHealth;

                    // Walk back into Central Hub
                    _hostCharacter.SetTestInteriorMoveOverride(new Vector2(0.0f, -1.0f));
                    _subStep = 1;
                }
                else if (_subStep == 1)
                {
                    if (_hostCharacter.CurrentInteriorRoom == InteriorRoomType.CentralHub && Mathf.Abs(_hostCharacter.ShipLocalPosition.Y) < 5.0f)
                    {
                        // Walk -X through Engine Door into Engine Room
                        _hostCharacter.SetTestInteriorMoveOverride(new Vector2(-1.0f, 0.0f));
                        _subStep = 2;
                    }
                }
                else if (_subStep == 2)
                {
                    if (_hostCharacter.CurrentInteriorRoom == InteriorRoomType.EngineRoom && _hostCharacter.ShipLocalPosition.X < -104.0f)
                    {
                        _hostCharacter.SetTestInteriorMoveOverride(null);
                        _hostShip.HandlePlayerInteriorInteractServer(1);

                        if (_hostShip.Interior.LastInteractedConsoleType != InteriorConsoleType.Engineering)
                        {
                            FailAndExit($"Expected Engineering Console interaction, got {_hostShip.Interior.LastInteractedConsoleType}.");
                            return;
                        }

                        if (_hostShip.Subsystems.CurrentEngineHealth <= _engHealthBefore)
                        {
                            FailAndExit($"Engineering Console failed to repair engine health! Before={_engHealthBefore}, Now={_hostShip.Subsystems.CurrentEngineHealth}");
                            return;
                        }

                        GD.Print($"[Milestone 3 Acceptance Test] Step 4 PASSED: Entered Engine Room and used Engineering Console (Engine Health {_engHealthBefore:F0} -> {_hostShip.Subsystems.CurrentEngineHealth:F0}).");
                        _step = 5;
                        _stepStartTime = _elapsed;
                        _subStep = 0;
                    }
                }
                break;

            case 5:
                // Walk +X back across the ship into Cockpit, mount Pilot Console, and resume flight controls
                if (_hostShip == null || _hostCharacter == null)
                {
                    return;
                }

                if (_subStep == 0)
                {
                    _hostCharacter.SetTestInteriorMoveOverride(new Vector2(1.0f, 0.0f));
                    _subStep = 1;
                }
                else if (_subStep == 1)
                {
                    if (_hostCharacter.CurrentInteriorRoom == InteriorRoomType.Cockpit && _hostCharacter.ShipLocalPosition.X > 104.0f)
                    {
                        _hostCharacter.SetTestInteriorMoveOverride(null);
                        _hostShip.HandlePlayerInteriorInteractServer(1);

                        if (_hostShip.PilotPeerId != 1 || !_hostCharacter.LocationContext.IsPiloting)
                        {
                            FailAndExit($"Failed to resume pilot controls via Pilot Console! Pilot={_hostShip.PilotPeerId}, IsPiloting={_hostCharacter.LocationContext.IsPiloting}");
                            return;
                        }

                        GD.Print("[Milestone 3 Acceptance Test] Step 5 PASSED: Returned to Cockpit, mounted Pilot Console, and resumed ship flight authority.");
                        GD.Print("=== [Milestone 3 Acceptance Test] ALL ACCEPTANCE CRITERIA PASSED ===");
                        CleanupAndExit(0);
                    }
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
        GD.Print($"[Milestone 3 Acceptance Test] Launched client subprocess '{clientName}' (PID {pid})");
    }

    private void FailAndExit(string message)
    {
        GD.PrintErr($"=== [Milestone 3 Acceptance Test] FAILED: {message} ===");
        CleanupAndExit(1);
    }

    private void CleanupAndExit(int exitCode)
    {
        if (_completed)
        {
            return;
        }
        _completed = true;

        _networkManager.ShutdownSession("Milestone 3 test complete");

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
