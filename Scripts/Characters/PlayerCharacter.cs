using Godot;
using SpaceExtraction.Ships;
using SpaceExtraction.World;

namespace SpaceExtraction.Characters;

/// <summary>
/// Authoritative player character body.
/// Supports both standalone floating movement (Milestone 0) and decoupled ship occupancy/piloting (Milestone 1+).
/// </summary>
public partial class PlayerCharacter : CharacterBody2D
{
    public const float DefaultMoveSpeed = 320.0f;

    public long PeerId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public int TeamId { get; private set; }
    public Color PlayerColor { get; private set; } = Colors.Cyan;
    public PlayerLocationContext LocationContext { get; private set; } = PlayerLocationContext.CreateWorldSpace();
    public uint LastProcessedInputTick { get; private set; }

    public Vector2 AuthoritativePosition => _authoritativePosition;
    public Vector2 AuthoritativeVelocity => _authoritativeVelocity;

    private Vector2 _authoritativePosition;
    private Vector2 _authoritativeVelocity;
    private Vector2 _currentServerInput;
    private Vector2? _testInputOverride;
    private uint _localInputTick;

    private Label? _nameLabel;
    private Camera2D? _camera;
    private CollisionShape2D? _collisionShape;

    public void Initialize(PlayerIdentity identity, Vector2 spawnPosition)
    {
        Name = $"Player_{identity.PeerId}";
        PeerId = identity.PeerId;
        DisplayName = identity.DisplayName;
        TeamId = identity.TeamId;
        PlayerColor = identity.PlayerColor;

        Position = spawnPosition;
        _authoritativePosition = spawnPosition;
        _authoritativeVelocity = Vector2.Zero;

        // Authoritative server (peer 1) owns simulation for every character
        SetMultiplayerAuthority(1);

        if (_nameLabel != null)
        {
            _nameLabel.Text = DisplayName;
        }

        RefreshOccupancyPresentation();
        QueueRedraw();
    }

    public void SetShipOccupancy(int shipId, bool isPiloting, NodePath shipPath)
    {
        if (shipId > 0)
        {
            LocationContext = PlayerLocationContext.CreateShipOccupancy(shipId, isPiloting, shipPath);
        }
        else
        {
            LocationContext = PlayerLocationContext.CreateWorldSpace();
        }

        RefreshOccupancyPresentation();
        QueueRedraw();
    }

    public void ClearShipOccupancy()
    {
        LocationContext = PlayerLocationContext.CreateWorldSpace();
        RefreshOccupancyPresentation();
        QueueRedraw();
    }

    public void SetTestInputOverride(Vector2? direction)
    {
        _testInputOverride = direction;
    }

    public bool IsLocalPlayer()
    {
        if (PeerId <= 0)
        {
            return false;
        }

        if (!Multiplayer.HasMultiplayerPeer() || Multiplayer.MultiplayerPeer is OfflineMultiplayerPeer)
        {
            return false;
        }

        return PeerId == Multiplayer.GetUniqueId();
    }

    public override void _Ready()
    {
        MotionMode = MotionModeEnum.Floating;
        SetMultiplayerAuthority(1);

        _collisionShape = new CollisionShape2D
        {
            Shape = new CircleShape2D { Radius = 16.0f }
        };
        AddChild(_collisionShape);

        _nameLabel = new Label
        {
            Position = new Vector2(-60.0f, -42.0f),
            Size = new Vector2(120.0f, 24.0f),
            HorizontalAlignment = HorizontalAlignment.Center,
            Text = DisplayName
        };
        AddChild(_nameLabel);

        _camera = new Camera2D
        {
            Enabled = IsLocalPlayer() && LocationContext.Mode == PlayerLocationMode.WorldSpace
        };
        AddChild(_camera);

        RefreshOccupancyPresentation();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (LocationContext.Mode == PlayerLocationMode.ShipInterior)
        {
            // While aboard/piloting a ship in Milestone 1 (pre-interiors), the ship renders the exterior hull.
            return;
        }

        DrawCircle(Vector2.Zero, 16.0f, PlayerColor);
        DrawCircle(Vector2.Zero, 8.0f, new Color(0.08f, 0.1f, 0.14f, 1.0f));

        Vector2 heading = Velocity.LengthSquared() > 1.0f ? Velocity.Normalized() : Vector2.Right;
        DrawLine(Vector2.Zero, heading * 22.0f, Colors.White, 3.0f);
        DrawArc(Vector2.Zero, 16.0f, 0f, Mathf.Tau, 32, Colors.White, 1.5f);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Multiplayer.HasMultiplayerPeer() || Multiplayer.MultiplayerPeer is OfflineMultiplayerPeer)
        {
            return;
        }

        // If occupying a ship in Milestone 1 (unless direct character test input override is active for M0 tests),
        // track the occupied ship's world position so player identity and ship occupancy stay synchronized.
        if (LocationContext.Mode == PlayerLocationMode.ShipInterior && _testInputOverride == null)
        {
            Ship? occupiedShip = FindOccupiedShip();
            if (occupiedShip != null)
            {
                GlobalPosition = occupiedShip.GlobalPosition;
                Velocity = occupiedShip.Velocity;
                _authoritativePosition = GlobalPosition;
                _authoritativeVelocity = Velocity;
                return;
            }
        }

        if (IsLocalPlayer())
        {
            Vector2 inputDir = _testInputOverride ?? ReadKeyboardInput();
            _localInputTick++;

            if (Multiplayer.IsServer())
            {
                ApplyServerInput(PeerId, inputDir, _localInputTick);
            }
            else
            {
                RpcId(1, MethodName.SubmitInputRpc, inputDir, _localInputTick);
            }
        }

        if (Multiplayer.IsServer())
        {
            Velocity = _currentServerInput * DefaultMoveSpeed;
            MoveAndSlide();
            _authoritativePosition = GlobalPosition;
            _authoritativeVelocity = Velocity;

            Rpc(MethodName.SyncAuthoritativeStateRpc, _authoritativePosition, _authoritativeVelocity, LastProcessedInputTick);
        }
        else
        {
            float dist = GlobalPosition.DistanceTo(_authoritativePosition);
            if (dist > 120.0f)
            {
                GlobalPosition = _authoritativePosition;
            }
            else
            {
                GlobalPosition = GlobalPosition.Lerp(_authoritativePosition, Mathf.Clamp((float)delta * 18.0f, 0f, 1f));
            }
            Velocity = _authoritativeVelocity;
        }

        if (Velocity.LengthSquared() > 0.1f)
        {
            QueueRedraw();
        }
    }

    public void ApplyServerInput(long senderPeerId, Vector2 rawDirection, uint tick)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        if (senderPeerId != PeerId)
        {
            return;
        }

        Vector2 sanitized = PlayerInputState.SanitizeDirection(rawDirection);
        if (tick >= LastProcessedInputTick || LastProcessedInputTick == 0)
        {
            _currentServerInput = sanitized;
            LastProcessedInputTick = tick;
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    public void SubmitInputRpc(Vector2 moveDirection, uint tick)
    {
        long senderId = Multiplayer.GetRemoteSenderId();
        if (senderId == 0)
        {
            senderId = Multiplayer.GetUniqueId();
        }

        ApplyServerInput(senderId, moveDirection, tick);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void SyncAuthoritativeStateRpc(Vector2 position, Vector2 velocity, uint processedTick)
    {
        if (!Multiplayer.IsServer())
        {
            if (processedTick >= LastProcessedInputTick || LastProcessedInputTick == 0)
            {
                _authoritativePosition = position;
                _authoritativeVelocity = velocity;
                LastProcessedInputTick = processedTick;
            }
        }
    }

    private Ship? FindOccupiedShip()
    {
        if (LocationContext.OccupiedShipId <= 0)
        {
            return null;
        }

        if (!LocationContext.OccupiedShipPath.IsEmpty)
        {
            Ship? byPath = GetNodeOrNull<Ship>(LocationContext.OccupiedShipPath);
            if (byPath != null)
            {
                return byPath;
            }
        }

        if (GetParent()?.GetParent() is GameWorld world)
        {
            return world.GetShip(LocationContext.OccupiedShipId);
        }

        return null;
    }

    private void RefreshOccupancyPresentation()
    {
        bool inShip = LocationContext.Mode == PlayerLocationMode.ShipInterior;

        if (_collisionShape != null)
        {
            _collisionShape.Disabled = inShip;
        }

        if (_nameLabel != null)
        {
            _nameLabel.Visible = !inShip;
        }

        if (_camera != null)
        {
            _camera.Enabled = IsLocalPlayer() && !inShip;
        }
    }

    private static Vector2 ReadKeyboardInput()
    {
        Vector2 dir = Vector2.Zero;
        if (Input.IsPhysicalKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
        {
            dir.Y -= 1.0f;
        }
        if (Input.IsPhysicalKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
        {
            dir.Y += 1.0f;
        }
        if (Input.IsPhysicalKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
        {
            dir.X -= 1.0f;
        }
        if (Input.IsPhysicalKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
        {
            dir.X += 1.0f;
        }

        return dir.LengthSquared() > 0.0001f ? dir.Normalized() : Vector2.Zero;
    }
}
