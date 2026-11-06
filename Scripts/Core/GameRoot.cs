using System;
using Godot;
using SpaceExtraction.Characters;
using SpaceExtraction.Match;
using SpaceExtraction.Networking;
using SpaceExtraction.Ships;
using SpaceExtraction.Testing;
using SpaceExtraction.UI;
using SpaceExtraction.World;

namespace SpaceExtraction.Core;

/// <summary>
/// Root composition node for the game.
/// Wires together NetworkManager, MatchController, GameWorld, and NetworkHud,
/// and supports CLI arguments for multi-instance testing and automated acceptance tests.
/// </summary>
public partial class GameRoot : Node2D
{
    public NetworkManager NetworkManager { get; private set; } = null!;
    public MatchController MatchController { get; private set; } = null!;
    public GameWorld GameWorld { get; private set; } = null!;
    public NetworkHud NetworkHud { get; private set; } = null!;

    private bool _isTestClient;
    private bool _isTestShipClient;
    private Vector2 _testClientMoveDir = Vector2.Right;
    private double _testClientElapsed;

    public override void _Ready()
    {
        NetworkManager = new NetworkManager { Name = "NetworkManager" };
        AddChild(NetworkManager);

        GameWorld = new GameWorld { Name = "GameWorld" };
        AddChild(GameWorld);

        MatchController = new MatchController { Name = "MatchController" };
        AddChild(MatchController);
        MatchController.Initialize(NetworkManager, GameWorld);

        NetworkHud = new NetworkHud { Name = "NetworkHud" };
        AddChild(NetworkHud);
        NetworkHud.Initialize(NetworkManager, MatchController, GameWorld);

        ParseCommandLineArguments();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_isTestClient && !_isTestShipClient)
        {
            return;
        }

        _testClientElapsed += delta;
        if (_testClientElapsed > 25.0)
        {
            GetTree().Quit(0);
            return;
        }

        if (NetworkManager.IsOnline && NetworkManager.LocalPeerId > 0)
        {
            if (_isTestShipClient)
            {
                Ship? localShip = GameWorld.GetShipByPilot(NetworkManager.LocalPeerId);
                localShip?.SetTestInputOverride(new ShipControlInput
                {
                    Throttle = 1.0f,
                    Turn = 0.35f,
                    Brake = false,
                    Boost = false,
                    FirePrimary = false
                });
            }
            else if (_isTestClient)
            {
                PlayerCharacter? localChar = GameWorld.GetPlayerCharacter(NetworkManager.LocalPeerId);
                localChar?.SetTestInputOverride(_testClientMoveDir);
            }
        }
    }

    private void ParseCommandLineArguments()
    {
        string[] allArgs = GetCombinedCommandLineArgs();
        if (allArgs.Length == 0)
        {
            return;
        }

        bool runM0Test = false;
        bool runM1Test = false;
        bool runM2Test = false;
        bool runM3Test = false;
        bool runM4Test = false;
        bool startHost = false;
        bool startServer = false;
        bool startClient = false;
        int port = NetworkManager.DefaultPort;
        string address = "127.0.0.1";
        string playerName = $"Pilot-{GD.Randi() % 900 + 100}";
        float moveX = 1.0f;
        float moveY = 0.0f;

        foreach (string rawArg in allArgs)
        {
            string arg = rawArg.Trim();
            if (arg == "--test-m0")
            {
                runM0Test = true;
            }
            else if (arg == "--test-m1")
            {
                runM1Test = true;
            }
            else if (arg == "--test-m2")
            {
                runM2Test = true;
            }
            else if (arg == "--test-m3")
            {
                runM3Test = true;
            }
            else if (arg == "--test-m4")
            {
                runM4Test = true;
            }
            else if (arg == "--test-client")
            {
                _isTestClient = true;
                startClient = true;
            }
            else if (arg == "--test-ship-client")
            {
                _isTestShipClient = true;
                startClient = true;
            }
            else if (arg == "--host")
            {
                startHost = true;
            }
            else if (arg == "--server")
            {
                startServer = true;
            }
            else if (arg == "--client")
            {
                startClient = true;
            }
            else if (arg.StartsWith("--port=", StringComparison.OrdinalIgnoreCase) &&
                     int.TryParse(arg.Substring("--port=".Length), out int parsedPort))
            {
                port = parsedPort;
            }
            else if (arg.StartsWith("--address=", StringComparison.OrdinalIgnoreCase))
            {
                address = arg.Substring("--address=".Length);
            }
            else if (arg.StartsWith("--name=", StringComparison.OrdinalIgnoreCase))
            {
                playerName = arg.Substring("--name=".Length);
            }
            else if (arg.StartsWith("--move-x=", StringComparison.OrdinalIgnoreCase) &&
                     float.TryParse(arg.Substring("--move-x=".Length), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float px))
            {
                moveX = px;
            }
            else if (arg.StartsWith("--move-y=", StringComparison.OrdinalIgnoreCase) &&
                     float.TryParse(arg.Substring("--move-y=".Length), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float py))
            {
                moveY = py;
            }
        }

        if (runM0Test)
        {
            var testRunner = new Milestone0AcceptanceTest { Name = "Milestone0AcceptanceTest" };
            testRunner.Initialize(NetworkManager, MatchController, GameWorld);
            AddChild(testRunner);
            return;
        }

        if (runM1Test)
        {
            var testRunner = new Milestone1AcceptanceTest { Name = "Milestone1AcceptanceTest" };
            testRunner.Initialize(NetworkManager, MatchController, GameWorld);
            AddChild(testRunner);
            return;
        }

        if (runM2Test)
        {
            var testRunner = new Milestone2AcceptanceTest { Name = "Milestone2AcceptanceTest" };
            testRunner.Initialize(NetworkManager, MatchController, GameWorld);
            AddChild(testRunner);
            return;
        }

        if (runM3Test)
        {
            var testRunner = new Milestone3AcceptanceTest { Name = "Milestone3AcceptanceTest" };
            testRunner.Initialize(NetworkManager, MatchController, GameWorld);
            AddChild(testRunner);
            return;
        }

        if (runM4Test)
        {
            var testRunner = new Milestone4AcceptanceTest { Name = "Milestone4AcceptanceTest" };
            testRunner.Initialize(NetworkManager, MatchController, GameWorld);
            AddChild(testRunner);
            return;
        }

        _testClientMoveDir = new Vector2(moveX, moveY);

        if (startServer)
        {
            NetworkManager.StartDedicatedServer(port);
        }
        else if (startHost)
        {
            NetworkManager.StartHost(port, playerName);
        }
        else if (startClient)
        {
            NetworkManager.JoinServer(address, port, playerName);
        }
    }

    private static string[] GetCombinedCommandLineArgs()
    {
        string[] cmdArgs = OS.GetCmdlineArgs();
        string[] userArgs = OS.GetCmdlineUserArgs();
        var combined = new string[cmdArgs.Length + userArgs.Length];
        Array.Copy(cmdArgs, 0, combined, 0, cmdArgs.Length);
        Array.Copy(userArgs, 0, combined, cmdArgs.Length, userArgs.Length);
        return combined;
    }
}
