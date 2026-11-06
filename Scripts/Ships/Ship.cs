using System;
using System.Collections.Generic;
using Godot;
using SpaceExtraction.World;

namespace SpaceExtraction.Ships;

/// <summary>
/// Server-authoritative ship entity (Milestone 1).
/// Supports physics simulation, weapon firing, pilot input replication, and hull integrity tracking.
/// </summary>
public partial class Ship : CharacterBody2D
{
    public int ShipId { get; private set; }
    public string ShipName { get; private set; } = "Corvette";
    public ShipClassType ShipClass { get; private set; } = ShipClassType.Corvette;
    public int OwnerTeamId { get; private set; }
    public long PilotPeerId { get; private set; }
    public bool IsNpcTarget { get; private set; }
    public Color ShipColor { get; private set; } = Colors.Cyan;
    public ShipMovementProfile MovementProfile { get; private set; } = ShipMovementProfile.CreateCorvette();
    public ShipMovement Movement { get; private set; } = null!;
    public ShipHull Hull { get; private set; } = null!;
    public ShipWeapon PrimaryWeapon { get; private set; } = null!;
    public uint LastProcessedInputTick { get; private set; }

    public Vector2 AuthoritativePosition => _authoritativePosition;
    public Vector2 AuthoritativeVelocity => _authoritativeVelocity;
    public float AuthoritativeRotation => _authoritativeRotation;
    public IReadOnlyCollection<long> OccupantPeerIds => _occupantPeerIds;

    private GameWorld? _gameWorld;
    private Vector2 _authoritativePosition;
    private Vector2 _authoritativeVelocity;
    private float _authoritativeRotation;
    private ShipControlInput _currentServerInput;
    private ShipControlInput? _testInputOverride;
    private uint _localInputTick;
    private readonly HashSet<long> _occupantPeerIds = new();

    private CollisionShape2D? _collisionShape;
    private Node2D? _overlayContainer;
    private Label? _infoLabel;
    private Camera2D? _camera;

    private static readonly Vector2[] BodyPolygon =
    {
        new(32.0f, 0.0f),
        new(10.0f, -20.0f),
        new(-26.0f, -22.0f),
        new(-20.0f, 0.0f),
        new(-26.0f, 22.0f),
        new(10.0f, 20.0f)
    };

    private static readonly Vector2[] CanopyPolygon =
    {
        new(12.0f, 0.0f),
        new(0.0f, -6.0f),
        new(-10.0f, 0.0f),
        new(0.0f, 6.0f)
    };

    private readonly Vector2[] _thrusterFlamePolygon =
    {
        new(-20.0f, -7.0f),
        new(-34.0f, 0.0f),
        new(-20.0f, 7.0f)
    };

    public Ship()
    {
        Movement = new ShipMovement(MovementProfile);
        Hull = new ShipHull(MovementProfile.MaxHullIntegrity);
        PrimaryWeapon = new ShipWeapon();
    }

    public void Initialize(
        GameWorld gameWorld,
        int shipId,
        string shipName,
        ShipClassType shipClass,
        int ownerTeamId,
        long pilotPeerId,
        bool isNpcTarget,
        Color shipColor,
        Vector2 spawnPosition,
        float spawnRotation = 0f)
    {
        Name = $"Ship_{shipId}";
        _gameWorld = gameWorld;
        ShipId = shipId;
        ShipName = shipName;
        ShipClass = shipClass;
        OwnerTeamId = ownerTeamId;
        IsNpcTarget = isNpcTarget;
        ShipColor = shipColor;

        if (Hull != null)
        {
            Hull.HullChanged -= OnHullChanged;
        }

        MovementProfile = ShipMovementProfile.FromClass(shipClass);
        Movement = new ShipMovement(MovementProfile);
        Hull = new ShipHull(MovementProfile.MaxHullIntegrity);
        Hull.HullChanged += OnHullChanged;
        PrimaryWeapon = new ShipWeapon();

        Position = spawnPosition;
        Rotation = spawnRotation;
        _authoritativePosition = spawnPosition;
        _authoritativeRotation = spawnRotation;
        _authoritativeVelocity = Vector2.Zero;

        SetPilot(pilotPeerId);
        SetMultiplayerAuthority(1);

        if (_collisionShape?.Shape is CircleShape2D circle)
        {
            circle.Radius = MovementProfile.CollisionRadius;
        }

        UpdateOverlayText();
        QueueRedraw();
    }

    public void SetPilot(long pilotPeerId)
    {
        PilotPeerId = pilotPeerId;
        if (pilotPeerId > 0)
        {
            _occupantPeerIds.Add(pilotPeerId);
        }

        if (_camera != null)
        {
            _camera.Enabled = IsLocalPilot();
        }
    }

    public void AddOccupant(long peerId)
    {
        if (peerId > 0)
        {
            _occupantPeerIds.Add(peerId);
        }
    }

    public void RemoveOccupant(long peerId)
    {
        _occupantPeerIds.Remove(peerId);
        if (PilotPeerId == peerId)
        {
            PilotPeerId = 0;
            if (_camera != null)
            {
                _camera.Enabled = false;
            }
        }
    }

    public bool IsLocalPilot()
    {
        return PilotPeerId > 0
            && Multiplayer.HasMultiplayerPeer()
            && Multiplayer.MultiplayerPeer is not OfflineMultiplayerPeer
            && PilotPeerId == Multiplayer.GetUniqueId();
    }

    public void SetTestInputOverride(ShipControlInput? input)
    {
        _testInputOverride = input;
    }

    public void ApplyServerPilotInput(long senderPeerId, ShipControlInput rawInput)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        // Anti-spoofing check: input must originate from the assigned pilot peer
        if (PilotPeerId <= 0 || senderPeerId != PilotPeerId)
        {
            return;
        }

        ShipControlInput sanitized = rawInput.Sanitized();
        if (sanitized.Tick >= LastProcessedInputTick || LastProcessedInputTick == 0)
        {
            _currentServerInput = sanitized;
            LastProcessedInputTick = sanitized.Tick;
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    public void SubmitPilotInputRpc(float throttle, float turn, bool brake, bool boost, bool firePrimary, uint tick)
    {
        long senderId = Multiplayer.GetRemoteSenderId();
        if (senderId == 0)
        {
            senderId = Multiplayer.GetUniqueId();
        }

        ApplyServerPilotInput(senderId, new ShipControlInput
        {
            Throttle = throttle,
            Turn = turn,
            Brake = brake,
            Boost = boost,
            FirePrimary = firePrimary,
            Tick = tick
        });
    }

    public void ApplyHullDamageServer(float amount, int attackerShipId = 0)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        Hull.ApplyDamage(amount, attackerShipId);
        Rpc(MethodName.SyncHullStateRpc, Hull.CurrentHull, Hull.MaxHull, Hull.IsDisabled);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncHullStateRpc(float currentHull, float maxHull, bool isDisabled)
    {
        Hull.SetStateFromReplication(currentHull, maxHull, isDisabled);
        UpdateOverlayText();
        QueueRedraw();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void SyncShipStateRpc(
        Vector2 position,
        Vector2 velocity,
        float rotationRadians,
        bool isBoosting,
        float boostCooldownRemaining,
        uint processedTick)
    {
        if (!Multiplayer.IsServer())
        {
            if (processedTick >= LastProcessedInputTick || LastProcessedInputTick == 0)
            {
                _authoritativePosition = position;
                _authoritativeVelocity = velocity;
                _authoritativeRotation = rotationRadians;
                LastProcessedInputTick = processedTick;
                Movement.SetStateFromReplication(isBoosting, boostCooldownRemaining);
            }
        }
    }

    public override void _Ready()
    {
        MotionMode = MotionModeEnum.Floating;
        SetMultiplayerAuthority(1);

        if (_gameWorld == null && GetParent()?.GetParent() is GameWorld world)
        {
            _gameWorld = world;
        }

        MovementProfile ??= ShipMovementProfile.CreateCorvette();
        Movement ??= new ShipMovement(MovementProfile);
        if (Hull == null)
        {
            Hull = new ShipHull(MovementProfile.MaxHullIntegrity);
            Hull.HullChanged += OnHullChanged;
        }
        PrimaryWeapon ??= new ShipWeapon();

        _collisionShape = new CollisionShape2D
        {
            Shape = new CircleShape2D { Radius = MovementProfile.CollisionRadius }
        };
        AddChild(_collisionShape);

        _overlayContainer = new Node2D { Name = "OverlayContainer" };
        AddChild(_overlayContainer);

        _infoLabel = new Label
        {
            Position = new Vector2(-90.0f, -54.0f),
            Size = new Vector2(180.0f, 24.0f),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _overlayContainer.AddChild(_infoLabel);

        _camera = new Camera2D
        {
            Enabled = IsLocalPilot()
        };
        AddChild(_camera);

        UpdateOverlayText();
        QueueRedraw();
    }

    public override void _ExitTree()
    {
        if (Hull != null)
        {
            Hull.HullChanged -= OnHullChanged;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Multiplayer.HasMultiplayerPeer() || Multiplayer.MultiplayerPeer is OfflineMultiplayerPeer)
        {
            return;
        }

        float dt = (float)delta;

        if (IsLocalPilot())
        {
            ShipControlInput input = _testInputOverride ?? ReadPilotKeyboardInput();
            _localInputTick++;
            input.Tick = _localInputTick;

            if (Multiplayer.IsServer())
            {
                ApplyServerPilotInput(PilotPeerId, input);
            }
            else
            {
                RpcId(1, MethodName.SubmitPilotInputRpc, input.Throttle, input.Turn, input.Brake, input.Boost, input.FirePrimary, input.Tick);
            }
        }
        else if (_testInputOverride.HasValue && Multiplayer.IsServer())
        {
            _localInputTick++;
            ShipControlInput testInput = _testInputOverride.Value;
            testInput.Tick = _localInputTick;
            _currentServerInput = testInput.Sanitized();
            LastProcessedInputTick = testInput.Tick;
        }

        if (Multiplayer.IsServer())
        {
            PrimaryWeapon.StepCooldown(dt);
            float rot = Rotation;
            Vector2 vel = Velocity;
            Movement.StepPhysics(_currentServerInput, Hull.IsDisabled, dt, ref rot, ref vel);
            Rotation = rot;
            Velocity = vel;
            MoveAndSlide();

            if (_gameWorld != null)
            {
                Rect2 innerBounds = _gameWorld.WorldBounds.Grow(-MovementProfile.CollisionRadius);
                GlobalPosition = new Vector2(
                    Mathf.Clamp(GlobalPosition.X, innerBounds.Position.X, innerBounds.End.X),
                    Mathf.Clamp(GlobalPosition.Y, innerBounds.Position.Y, innerBounds.End.Y));
            }

            _authoritativePosition = GlobalPosition;
            _authoritativeVelocity = Velocity;
            _authoritativeRotation = Rotation;

            if (PrimaryWeapon.TryFire(_currentServerInput.FirePrimary, Hull.IsDisabled, GlobalPosition, Rotation, out Vector2 muzzlePos, out Vector2 fireDir) && _gameWorld != null)
            {
                _gameWorld.SpawnProjectileServer(ShipId, OwnerTeamId, muzzlePos, fireDir, PrimaryWeapon.ProjectileSpeed, PrimaryWeapon.ProjectileDamage, PrimaryWeapon.ProjectileLifetime, ShipColor);
            }

            Rpc(MethodName.SyncShipStateRpc, _authoritativePosition, _authoritativeVelocity, _authoritativeRotation, Movement.IsBoosting, Movement.BoostCooldownRemaining, LastProcessedInputTick);
        }
        else
        {
            float dist = GlobalPosition.DistanceTo(_authoritativePosition);
            if (dist > 160.0f)
            {
                GlobalPosition = _authoritativePosition;
            }
            else
            {
                GlobalPosition = GlobalPosition.Lerp(_authoritativePosition, Mathf.Clamp(dt * 18.0f, 0f, 1f));
            }

            Rotation = Mathf.LerpAngle(Rotation, _authoritativeRotation, Mathf.Clamp(dt * 18.0f, 0f, 1f));
            Velocity = _authoritativeVelocity;
        }

        if (_overlayContainer != null)
        {
            _overlayContainer.Rotation = -Rotation;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        bool disabled = Hull?.IsDisabled ?? false;
        Color baseColor = disabled ? ShipColor.Darkened(0.6f) : ShipColor;
        DrawColoredPolygon(BodyPolygon, baseColor);

        Color outlineColor = disabled ? Colors.DimGray : Colors.White;
        for (int i = 0; i < BodyPolygon.Length; i++)
        {
            Vector2 p1 = BodyPolygon[i];
            Vector2 p2 = BodyPolygon[(i + 1) % BodyPolygon.Length];
            DrawLine(p1, p2, outlineColor, 1.5f);
        }

        // Cockpit canopy
        Color canopyColor = disabled ? new Color(0.2f, 0.2f, 0.2f, 0.8f) : new Color(0.85f, 0.95f, 1.0f, 0.9f);
        DrawColoredPolygon(CanopyPolygon, canopyColor);

        // Forward gun barrel
        DrawLine(new Vector2(26.0f, 0.0f), new Vector2(38.0f, 0.0f), disabled ? Colors.DarkGray : Colors.LightGray, 3.0f);

        // Engine thruster glow
        bool isBoosting = Movement?.IsBoosting ?? false;
        bool isThrusting = _currentServerInput.Throttle > 0.05f || isBoosting;
        if (!disabled && isThrusting)
        {
            Color flameColor = isBoosting ? new Color(1.0f, 0.8f, 0.1f) : new Color(0.3f, 0.7f, 1.0f);
            float flameLength = isBoosting ? 26.0f : 14.0f;
            _thrusterFlamePolygon[1] = new Vector2(-20.0f - flameLength, 0.0f);
            DrawColoredPolygon(_thrusterFlamePolygon, flameColor);
        }

        // Hull integrity bar
        if (Hull != null)
        {
            float barWidth = 48.0f;
            float barHeight = 5.0f;
            Vector2 barPos = new Vector2(-barWidth / 2.0f, 30.0f);
            DrawRect(new Rect2(barPos, new Vector2(barWidth, barHeight)), new Color(0f, 0f, 0f, 0.6f), true);
            float fillWidth = barWidth * Hull.HullFraction;
            Color barColor = disabled ? Colors.DarkRed : (Hull.HullFraction > 0.4f ? Colors.LimeGreen : Colors.OrangeRed);
            if (fillWidth > 0f)
            {
                DrawRect(new Rect2(barPos, new Vector2(fillWidth, barHeight)), barColor, true);
            }
            DrawRect(new Rect2(barPos, new Vector2(barWidth, barHeight)), Colors.White, false, 1.0f);
        }
    }

    private void UpdateOverlayText()
    {
        if (_infoLabel == null || Hull == null)
        {
            return;
        }

        string status = Hull.IsDisabled ? " [DISABLED]" : string.Empty;
        _infoLabel.Text = $"{ShipName} ({(int)Hull.CurrentHull}/{(int)Hull.MaxHull}){status}";
    }

    private void OnHullChanged(float current, float max, bool disabled)
    {
        UpdateOverlayText();
        QueueRedraw();
    }

    private static ShipControlInput ReadPilotKeyboardInput()
    {
        float throttle = 0.0f;
        if (Input.IsPhysicalKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
        {
            throttle += 1.0f;
        }
        if (Input.IsPhysicalKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
        {
            throttle -= 1.0f;
        }

        float turn = 0.0f;
        if (Input.IsPhysicalKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
        {
            turn -= 1.0f;
        }
        if (Input.IsPhysicalKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
        {
            turn += 1.0f;
        }

        bool brake = Input.IsPhysicalKeyPressed(Key.X) || Input.IsPhysicalKeyPressed(Key.C);
        bool boost = Input.IsPhysicalKeyPressed(Key.Shift);
        bool firePrimary = Input.IsPhysicalKeyPressed(Key.Space)
            || Input.IsPhysicalKeyPressed(Key.F)
            || Input.IsMouseButtonPressed(MouseButton.Left);

        return new ShipControlInput
        {
            Throttle = throttle,
            Turn = turn,
            Brake = brake,
            Boost = boost,
            FirePrimary = firePrimary
        };
    }
}
