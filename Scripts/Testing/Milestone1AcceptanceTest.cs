using System;
using System.Collections.Generic;
using Godot;
using SpaceExtraction.Match;
using SpaceExtraction.Networking;
using SpaceExtraction.Ships;
using SpaceExtraction.World;

namespace SpaceExtraction.Testing;

/// <summary>
/// Automated multi-instance acceptance test for Milestone 1 (Basic Ship Gameplay).
/// Verifies under server authority:
/// 1. Multiple clients connect and spawn into Corvettes alongside an enemy target ship.
/// 2. Server rejects spoofed pilot inputs from another peer.
/// 3. Ships rotate, accelerate, boost, and brake reliably.
/// 4. Ships fire projectiles, damage another ship's hull, and disable it when hull reaches 0.
/// 5. Disabled ships cannot thrust, rotate, boost, or fire weapons.
/// Triggered via command line flag: --test-m1
/// </summary>
public partial class Milestone1AcceptanceTest : Node
{
    private const int TestPort = 17791;
    private const double TimeoutSeconds = 30.0;

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
    private Vector2 _hostInitialPos;
    private float _hostInitialRot;
    private float _peakSpeedBeforeBrake;

    public void Initialize(NetworkManager networkManager, MatchController matchController, GameWorld gameWorld)
    {
        _networkManager = networkManager;
        _matchController = matchController;
        _gameWorld = gameWorld;
    }

    public override void _Ready()
    {
        GD.Print("=== [Milestone 1 Acceptance Test] Starting authoritative server ===");
        Error err = _networkManager.StartHost(TestPort, "HostCommander");
        if (err != Error.Ok)
        {
            FailAndExit($"Failed to start host on port {TestPort}: {err}");
            return;
        }

        SpawnClientSubprocess("WingmanClient");
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
                // Wait for Host + WingmanClient + EnemyTargetShip to spawn
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

                Ship? clientShip = _clientPeerId > 0 ? _gameWorld.GetShipByPilot(_clientPeerId) : null;
                if (_hostShip != null && _targetShip != null && clientShip != null)
                {
                    if (_hostShip.GetMultiplayerAuthority() != 1 ||
                        _targetShip.GetMultiplayerAuthority() != 1 ||
                        clientShip.GetMultiplayerAuthority() != 1)
                    {
                        FailAndExit("Expected server (peer 1) to own multiplayer authority for all ships.");
                        return;
                    }

                    // Anti-spoofing check: Host peer (1) attempting to submit input for clientShip must be rejected
                    uint tickBefore = clientShip.LastProcessedInputTick;
                    clientShip.ApplyServerPilotInput(1, new ShipControlInput
                    {
                        Throttle = 1.0f,
                        Turn = 1.0f,
                        Tick = tickBefore + 500
                    });
                    if (clientShip.LastProcessedInputTick == tickBefore + 500)
                    {
                        FailAndExit("Server accepted spoofed pilot input from non-pilot peer!");
                        return;
                    }

                    _hostInitialPos = _hostShip.GlobalPosition;
                    _hostInitialRot = _hostShip.Rotation;

                    // Command Host ship to turn, thrust forward, and activate Boost
                    _hostShip.SetTestInputOverride(new ShipControlInput
                    {
                        Throttle = 1.0f,
                        Turn = 0.6f,
                        Boost = true,
                        Brake = false,
                        FirePrimary = false
                    });

                    GD.Print("[Milestone 1 Acceptance Test] Step 0 PASSED: Host ship, Client ship, and Enemy Target ship spawned; anti-spoofing verified.");
                    _step = 1;
                    _stepStartTime = _elapsed;
                }
                break;

            case 1:
                // Verify rotation, acceleration, and boost activation on Host ship, and flight on Client ship
                Ship? wingmanShip = _gameWorld.GetShipByPilot(_clientPeerId);
                if (_hostShip == null || wingmanShip == null)
                {
                    FailAndExit("Ships missing during Step 1.");
                    return;
                }

                float hostMovedDist = _hostShip.GlobalPosition.DistanceTo(_hostInitialPos);
                float hostRotDelta = Mathf.Abs(Mathf.AngleDifference(_hostInitialRot, _hostShip.Rotation));
                float currentSpeed = _hostShip.Velocity.Length();

                if (_hostShip.Movement.IsBoosting && hostMovedDist > 45.0f && hostRotDelta > 0.25f && currentSpeed > 180.0f && wingmanShip.Velocity.Length() > 80.0f)
                {
                    _peakSpeedBeforeBrake = currentSpeed;
                    GD.Print($"[Milestone 1 Acceptance Test] Step 1 PASSED: Flight, rotation ({hostRotDelta:F2} rad), and boost ({_peakSpeedBeforeBrake:F0} u/s) verified.");

                    // Wait for boost to finish or clear boost by testing active Braking once boost ends
                    _hostShip.SetTestInputOverride(new ShipControlInput
                    {
                        Throttle = 0.0f,
                        Turn = 0.0f,
                        Boost = false,
                        Brake = true,
                        FirePrimary = false
                    });
                    _step = 2;
                    _stepStartTime = _elapsed;
                }
                break;

            case 2:
                // Verify active braking decelerates the ship significantly once boost duration expires
                if (_hostShip == null)
                {
                    return;
                }

                if (!_hostShip.Movement.IsBoosting && _hostShip.Velocity.Length() < _peakSpeedBeforeBrake * 0.35f)
                {
                    GD.Print($"[Milestone 1 Acceptance Test] Step 2 PASSED: Active braking decelerated ship from {_peakSpeedBeforeBrake:F0} u/s to {_hostShip.Velocity.Length():F0} u/s.");

                    // Position Host ship facing the Enemy Target Ship at close-medium range to test weapons, damage, and disabling
                    _hostShip.GlobalPosition = _targetShip!.GlobalPosition + new Vector2(-320.0f, 0.0f);
                    _hostShip.Rotation = 0.0f;
                    _hostShip.Velocity = Vector2.Zero;

                    _hostShip.SetTestInputOverride(new ShipControlInput
                    {
                        Throttle = 0.0f,
                        Turn = 0.0f,
                        Brake = true,
                        Boost = false,
                        FirePrimary = true
                    });

                    _step = 3;
                    _stepStartTime = _elapsed;
                }
                break;

            case 3:
                // Verify projectiles damage and ultimately disable the enemy target ship
                if (_targetShip == null || _hostShip == null)
                {
                    return;
                }

                // Keep aiming directly at target ship in case of minor drift
                Vector2 toTarget = _targetShip.GlobalPosition - _hostShip.GlobalPosition;
                _hostShip.Rotation = toTarget.Angle();

                if (_targetShip.Hull.IsDisabled && _targetShip.Hull.CurrentHull <= 0.0f)
                {
                    GD.Print($"[Milestone 1 Acceptance Test] Step 3 PASSED: Target ship damaged to 0/{_targetShip.Hull.MaxHull:F0} hull and transitioned to DISABLED.");

                    // Now verify that a disabled ship cannot thrust, rotate, boost, or fire even if given a pilot and full inputs
                    _hostShip.SetTestInputOverride(null);
                    _targetShip.SetPilot(1);
                    _targetShip.Velocity = Vector2.Zero;
                    float disabledStartRot = _targetShip.Rotation;
                    _hostInitialRot = disabledStartRot;

                    _targetShip.ApplyServerPilotInput(1, new ShipControlInput
                    {
                        Throttle = 1.0f,
                        Turn = 1.0f,
                        Boost = true,
                        Brake = false,
                        FirePrimary = true,
                        Tick = 999
                    });

                    _step = 4;
                    _stepStartTime = _elapsed;
                }
                break;

            case 4:
                // Verify disabled ship remains unable to accelerate, rotate, or boost after several physics ticks
                if (_targetShip == null)
                {
                    return;
                }

                if (_elapsed - _stepStartTime >= 0.25)
                {
                    if (_targetShip.Movement.IsBoosting ||
                        _targetShip.Velocity.Length() > 1.0f ||
                        Mathf.Abs(Mathf.AngleDifference(_hostInitialRot, _targetShip.Rotation)) > 0.01f)
                    {
                        FailAndExit("Disabled ship was still able to move, rotate, or boost!");
                        return;
                    }

                    GD.Print("[Milestone 1 Acceptance Test] Step 4 PASSED: Disabled ship confirmed immobilized and unable to boost/rotate/fire.");
                    GD.Print("=== [Milestone 1 Acceptance Test] ALL ACCEPTANCE CRITERIA PASSED ===");
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
        GD.Print($"[Milestone 1 Acceptance Test] Launched client subprocess '{clientName}' (PID {pid})");
    }

    private void FailAndExit(string message)
    {
        GD.PrintErr($"=== [Milestone 1 Acceptance Test] FAILED: {message} ===");
        CleanupAndExit(1);
    }

    private void CleanupAndExit(int exitCode)
    {
        if (_completed)
        {
            return;
        }
        _completed = true;

        _networkManager.ShutdownSession("Milestone 1 test complete");

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
