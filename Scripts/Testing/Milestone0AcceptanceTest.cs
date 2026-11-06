using System;
using System.Collections.Generic;
using Godot;
using SpaceExtraction.Characters;
using SpaceExtraction.Match;
using SpaceExtraction.Networking;
using SpaceExtraction.World;

namespace SpaceExtraction.Testing;

/// <summary>
/// Automated, observable multi-instance acceptance test for Milestone 0.
/// Launches two real client child processes that connect over ENet UDP to the authoritative server,
/// verifies player registration, replication, anti-spoofing server authority, and movement synchronization.
/// Triggered via command line flag: --test-m0
/// </summary>
public partial class Milestone0AcceptanceTest : Node
{
    private const int TestPort = 17789;
    private const double TimeoutSeconds = 18.0;

    private NetworkManager _networkManager = null!;
    private MatchController _matchController = null!;
    private GameWorld _gameWorld = null!;

    private readonly List<int> _clientProcessIds = new();
    private double _elapsed;
    private int _step;
    private readonly Dictionary<long, Vector2> _initialPositions = new();
    private long _firstClientPeerId;
    private long _secondClientPeerId;
    private bool _completed;

    public void Initialize(NetworkManager networkManager, MatchController matchController, GameWorld gameWorld)
    {
        _networkManager = networkManager;
        _matchController = matchController;
        _gameWorld = gameWorld;
    }

    public override void _Ready()
    {
        GD.Print("=== [Milestone 0 Acceptance Test] Starting authoritative server ===");
        Error err = _networkManager.StartHost(TestPort, "ServerHostPilot");
        if (err != Error.Ok)
        {
            FailAndExit($"Failed to start host on port {TestPort}: {err}");
            return;
        }

        // Spawn two real headless client Godot processes that connect and transmit inputs
        SpawnClientSubprocess("AlphaClient", new Vector2(1f, 0f));
        SpawnClientSubprocess("BravoClient", new Vector2(0f, 1f));
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
            FailAndExit($"Timed out at step {_step}. Registered players: {_networkManager.Players.Count}");
            return;
        }

        switch (_step)
        {
            case 0:
                // Wait for Host (1) + AlphaClient + BravoClient = 3 connected players & characters spawned
                if (_networkManager.Players.Count >= 3 && _gameWorld.GetAllPlayerCharacters().Count >= 3)
                {
                    foreach (long peerId in _networkManager.Players.Keys)
                    {
                        if (peerId == 1)
                        {
                            continue;
                        }

                        if (_firstClientPeerId == 0)
                        {
                            _firstClientPeerId = peerId;
                        }
                        else if (_secondClientPeerId == 0)
                        {
                            _secondClientPeerId = peerId;
                        }
                    }

                    // Verify Criterion 3: Server owns authoritative state for all players
                    foreach (PlayerCharacter pc in _gameWorld.GetAllPlayerCharacters())
                    {
                        if (pc.GetMultiplayerAuthority() != 1)
                        {
                            FailAndExit($"Player {pc.PeerId} authority is {pc.GetMultiplayerAuthority()}, expected 1 (Server).");
                            return;
                        }
                        _initialPositions[pc.PeerId] = pc.GlobalPosition;
                    }

                    // Verify anti-spoofing: attempt to apply input for second client claiming to come from first client
                    PlayerCharacter? secondChar = _gameWorld.GetPlayerCharacter(_secondClientPeerId);
                    if (secondChar == null)
                    {
                        FailAndExit("Second client character missing.");
                        return;
                    }

                    uint tickBeforeSpoof = secondChar.LastProcessedInputTick;
                    secondChar.ApplyServerInput(_firstClientPeerId, new Vector2(-1f, -1f), tickBeforeSpoof + 500);
                    if (secondChar.LastProcessedInputTick == tickBeforeSpoof + 500)
                    {
                        FailAndExit("Server accepted spoofed input from wrong sender peer ID!");
                        return;
                    }

                    // Also drive host player locally
                    PlayerCharacter? hostChar = _gameWorld.GetPlayerCharacter(1);
                    hostChar?.SetTestInputOverride(new Vector2(-1f, 0.5f));

                    GD.Print($"[Milestone 0 Acceptance Test] Step 0 PASSED: 3 players registered & spawned, server authority & anti-spoofing verified.");
                    _step = 1;
                }
                break;

            case 1:
                // Verify all 3 players move reliably under server physics authority
                bool allMoved = true;
                foreach (var kvp in _initialPositions)
                {
                    PlayerCharacter? pc = _gameWorld.GetPlayerCharacter(kvp.Key);
                    if (pc == null || pc.GlobalPosition.DistanceTo(kvp.Value) < 40.0f)
                    {
                        allMoved = false;
                        break;
                    }
                }

                if (allMoved && _matchController.CurrentPhase == MatchPhase.InProgress)
                {
                    foreach (var kvp in _initialPositions)
                    {
                        PlayerCharacter pc = _gameWorld.GetPlayerCharacter(kvp.Key)!;
                        GD.Print($"[Milestone 0 Acceptance Test] Player {pc.PeerId} ({pc.DisplayName}) moved from {kvp.Value} to {pc.GlobalPosition} (Tick={pc.LastProcessedInputTick})");
                    }

                    GD.Print("=== [Milestone 0 Acceptance Test] ALL ACCEPTANCE CRITERIA PASSED ===");
                    CleanupAndExit(0);
                }
                break;
        }
    }

    private void SpawnClientSubprocess(string clientName, Vector2 moveDir)
    {
        string exePath = OS.GetExecutablePath();
        string[] args =
        {
            "--headless",
            "--path", ProjectSettings.GlobalizePath("res://"),
            "--",
            "--test-client",
            $"--port={TestPort}",
            $"--name={clientName}",
            $"--move-x={moveDir.X:F2}",
            $"--move-y={moveDir.Y:F2}"
        };

        int pid = OS.CreateProcess(exePath, args);
        if (pid <= 0)
        {
            FailAndExit($"Failed to launch test client subprocess for {clientName}");
            return;
        }

        _clientProcessIds.Add(pid);
        GD.Print($"[Milestone 0 Acceptance Test] Launched client subprocess '{clientName}' (PID {pid})");
    }

    private void FailAndExit(string message)
    {
        GD.PrintErr($"=== [Milestone 0 Acceptance Test] FAILED: {message} ===");
        CleanupAndExit(1);
    }

    private void CleanupAndExit(int exitCode)
    {
        if (_completed)
        {
            return;
        }
        _completed = true;

        _networkManager.ShutdownSession("Test complete");

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
