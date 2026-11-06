using System;
using System.Collections.Generic;
using Godot;
using SpaceExtraction.World;

namespace SpaceExtraction.Ships;

/// <summary>
/// Server-authoritative ship entity (Milestones 1 & 2).
/// Supports physics simulation, weapon switching/reloading, turret operations (manual & automated),
/// pilot/gunner station role toggles, discrete subsystem damage tracking, and state replication.
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
    public ShipSubsystems Subsystems { get; private set; } = new();
    public ShipTurret Turret { get; private set; } = new();

    public IReadOnlyList<ShipWeapon> Weapons => _weapons;
    private ShipWeapon[] _weapons = new[]
    {
        ShipWeapon.CreateAutocannon(),
        ShipWeapon.CreateRailgun(),
        ShipWeapon.CreateEmpCannon()
    };
    public int ActiveWeaponIndex { get; private set; }
    public ShipWeapon PrimaryWeapon => _weapons[ActiveWeaponIndex];
    public ShipSubsystemType SelectedTargetSubsystem { get; private set; } = ShipSubsystemType.Engines;

    public uint LastProcessedInputTick { get; private set; }
    public uint LastProcessedTurretTick { get; private set; }

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

    private TurretControlInput _currentTurretInput;
    private TurretControlInput? _testTurretInputOverride;
    private uint _localTurretTick;
    private bool _prevToggleKey;

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
        Subsystems = new ShipSubsystems();
        Turret = new ShipTurret();
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

        if (Subsystems != null)
        {
            Subsystems.SubsystemsChanged -= OnSubsystemsChanged;
        }

        MovementProfile = ShipMovementProfile.FromClass(shipClass);
        Movement = new ShipMovement(MovementProfile);
        Hull = new ShipHull(MovementProfile.MaxHullIntegrity);
        Hull.HullChanged += OnHullChanged;

        Subsystems = new ShipSubsystems();
        Subsystems.SubsystemsChanged += OnSubsystemsChanged;

        _weapons = new[]
        {
            ShipWeapon.CreateAutocannon(),
            ShipWeapon.CreateRailgun(),
            ShipWeapon.CreateEmpCannon()
        };
        ActiveWeaponIndex = 0;
        SelectedTargetSubsystem = ShipSubsystemType.Engines;

        Turret = new ShipTurret();

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

    public void SelectWeaponSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < _weapons.Length)
        {
            ActiveWeaponIndex = slotIndex;
        }
    }

    public void SetPilot(long pilotPeerId)
    {
        PilotPeerId = pilotPeerId;
        if (pilotPeerId > 0)
        {
            _occupantPeerIds.Add(pilotPeerId);
        }
        else
        {
            _currentServerInput = default;
        }

        if (_camera != null)
        {
            _camera.Enabled = IsLocalPilot() || IsLocalTurretGunner();
        }
    }

    public void SetTurretGunner(long gunnerPeerId)
    {
        Turret.SetGunner(gunnerPeerId);
        if (gunnerPeerId > 0)
        {
            _occupantPeerIds.Add(gunnerPeerId);
        }
        else
        {
            _currentTurretInput = default;
        }

        if (_camera != null)
        {
            _camera.Enabled = IsLocalPilot() || IsLocalTurretGunner();
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
            _currentServerInput = default;
        }
        if (Turret.GunnerPeerId == peerId)
        {
            Turret.SetGunner(0);
            _currentTurretInput = default;
        }

        if (_camera != null)
        {
            _camera.Enabled = IsLocalPilot() || IsLocalTurretGunner();
        }
    }

    public bool IsLocalPilot()
    {
        return PilotPeerId > 0
            && Multiplayer.HasMultiplayerPeer()
            && Multiplayer.MultiplayerPeer is not OfflineMultiplayerPeer
            && PilotPeerId == Multiplayer.GetUniqueId();
    }

    public bool IsLocalTurretGunner()
    {
        return Turret.GunnerPeerId > 0
            && Multiplayer.HasMultiplayerPeer()
            && Multiplayer.MultiplayerPeer is not OfflineMultiplayerPeer
            && Turret.GunnerPeerId == Multiplayer.GetUniqueId();
    }

    public void SetStationRolesServer(long pilotPeerId, long gunnerPeerId)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        Rpc(MethodName.SyncCrewRolesRpc, pilotPeerId, gunnerPeerId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestToggleStationRoleRpc()
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        long senderId = Multiplayer.GetRemoteSenderId();
        if (senderId == 0)
        {
            senderId = Multiplayer.GetUniqueId();
        }

        if (!_occupantPeerIds.Contains(senderId) && PilotPeerId != senderId && Turret.GunnerPeerId != senderId)
        {
            return;
        }

        if (PilotPeerId == senderId && Turret.GunnerPeerId == 0)
        {
            SetStationRolesServer(0, senderId);
        }
        else if (Turret.GunnerPeerId == senderId && PilotPeerId == 0)
        {
            SetStationRolesServer(senderId, 0);
        }
        else if (Turret.GunnerPeerId == 0 && PilotPeerId != senderId)
        {
            SetStationRolesServer(PilotPeerId, senderId);
        }
        else if (PilotPeerId == 0 && Turret.GunnerPeerId != senderId)
        {
            SetStationRolesServer(senderId, Turret.GunnerPeerId);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncCrewRolesRpc(long pilotPeerId, long gunnerPeerId)
    {
        PilotPeerId = pilotPeerId;
        if (pilotPeerId > 0)
        {
            _occupantPeerIds.Add(pilotPeerId);
        }
        else
        {
            _currentServerInput = default;
        }

        Turret.SetGunner(gunnerPeerId);
        if (gunnerPeerId > 0)
        {
            _occupantPeerIds.Add(gunnerPeerId);
        }
        else
        {
            _currentTurretInput = default;
        }

        if (_camera != null)
        {
            _camera.Enabled = IsLocalPilot() || IsLocalTurretGunner();
        }

        UpdateOverlayText();
        QueueRedraw();
    }

    public void SetTestInputOverride(ShipControlInput? input)
    {
        _testInputOverride = input;
    }

    public void SetTestTurretInputOverride(TurretControlInput? input)
    {
        _testTurretInputOverride = input;
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

            if (sanitized.SelectedWeaponSlot >= 0 && sanitized.SelectedWeaponSlot < _weapons.Length)
            {
                ActiveWeaponIndex = sanitized.SelectedWeaponSlot;
            }

            if (rawInput.TargetSubsystem != ShipSubsystemType.None)
            {
                SelectedTargetSubsystem = sanitized.TargetSubsystem;
            }

            if (sanitized.ReloadRequested)
            {
                PrimaryWeapon.StartReload();
            }
        }
    }

    public void ApplyServerTurretInput(long senderPeerId, TurretControlInput rawInput)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        // Anti-spoofing check: input must originate from the assigned manual gunner peer
        if (Turret.GunnerPeerId <= 0 || senderPeerId != Turret.GunnerPeerId)
        {
            return;
        }

        TurretControlInput sanitized = rawInput.Sanitized();
        if (sanitized.Tick >= LastProcessedTurretTick || LastProcessedTurretTick == 0)
        {
            _currentTurretInput = sanitized;
            LastProcessedTurretTick = sanitized.Tick;
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    public void SubmitPilotCombatInputRpc(
        float throttle,
        float turn,
        bool brake,
        bool boost,
        bool firePrimary,
        bool reloadRequested,
        int selectedWeaponSlot,
        int targetSubsystemInt,
        uint tick)
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
            ReloadRequested = reloadRequested,
            SelectedWeaponSlot = selectedWeaponSlot,
            TargetSubsystem = (ShipSubsystemType)targetSubsystemInt,
            Tick = tick
        });
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
            ReloadRequested = false,
            SelectedWeaponSlot = -1,
            TargetSubsystem = SelectedTargetSubsystem,
            Tick = tick
        });
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    public void SubmitTurretInputRpc(float aimAngleRadians, bool fireTurret, int targetSubsystemInt, uint tick)
    {
        long senderId = Multiplayer.GetRemoteSenderId();
        if (senderId == 0)
        {
            senderId = Multiplayer.GetUniqueId();
        }

        ApplyServerTurretInput(senderId, new TurretControlInput
        {
            AimAngleRadians = aimAngleRadians,
            FireTurret = fireTurret,
            TargetSubsystem = (ShipSubsystemType)targetSubsystemInt,
            Tick = tick
        });
    }

    public const float BowSubsystemZoneThresholdX = 9.0f;
    public const float SternSubsystemZoneThresholdX = -9.0f;

    /// <summary>
    /// Resolves which subsystem occupies the impacted hull section in the ship's local coordinate space:
    /// - Bow / Front (local X > +9) -> Weapons
    /// - Midships / Dorsal (-9 <= local X <= +9) -> Turret
    /// - Stern / Rear (local X < -9) -> Engines
    /// </summary>
    public static ShipSubsystemType ResolveSubsystemAtLocalPoint(Vector2 localImpactPoint)
    {
        if (localImpactPoint.X > BowSubsystemZoneThresholdX)
        {
            return ShipSubsystemType.Weapons;
        }
        if (localImpactPoint.X < SternSubsystemZoneThresholdX)
        {
            return ShipSubsystemType.Engines;
        }
        return ShipSubsystemType.Turret;
    }

    public ShipSubsystemType ResolveSubsystemAtWorldPoint(Vector2 worldImpactPoint)
    {
        Vector2 localImpactPoint = (worldImpactPoint - GlobalPosition).Rotated(-Rotation);
        return ResolveSubsystemAtLocalPoint(localImpactPoint);
    }

    public void ApplyPositionalCombatHitServer(Vector2 worldImpactPoint, float hullDamage, float subsystemDamage, int attackerShipId = 0)
    {
        ShipSubsystemType hitSubsystem = ResolveSubsystemAtWorldPoint(worldImpactPoint);
        ApplyCombatHitServer(hullDamage, subsystemDamage, hitSubsystem, attackerShipId);
    }

    public void ApplyCombatHitServer(float hullDamage, float subsystemDamage, ShipSubsystemType targetSubsystem, int attackerShipId = 0)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        if (hullDamage > 0.0f)
        {
            Hull.ApplyDamage(hullDamage, attackerShipId);
            Rpc(MethodName.SyncHullStateRpc, Hull.CurrentHull, Hull.MaxHull, Hull.IsDisabled);
        }

        if (subsystemDamage > 0.0f && targetSubsystem != ShipSubsystemType.None)
        {
            Subsystems.ApplySubsystemDamage(targetSubsystem, subsystemDamage);
            Rpc(MethodName.SyncSubsystemsStateRpc, Subsystems.CurrentEngineHealth, Subsystems.CurrentWeaponsHealth, Subsystems.CurrentTurretHealth);
        }
    }

    public void ApplyHullDamageServer(float amount, int attackerShipId = 0)
    {
        ApplyCombatHitServer(amount, 0.0f, ShipSubsystemType.None, attackerShipId);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncHullStateRpc(float currentHull, float maxHull, bool isDisabled)
    {
        Hull.SetStateFromReplication(currentHull, maxHull, isDisabled);
        UpdateOverlayText();
        QueueRedraw();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncSubsystemsStateRpc(float engineHealth, float weaponsHealth, float turretHealth)
    {
        Subsystems.SetStateFromReplication(engineHealth, weaponsHealth, turretHealth);
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
        uint processedTick,
        int activeWeaponIndex,
        int primaryAmmo,
        bool isReloading,
        float reloadRemaining,
        long turretGunnerPeerId,
        float turretAimRotation,
        int autoTargetShipId)
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

                if (activeWeaponIndex >= 0 && activeWeaponIndex < _weapons.Length)
                {
                    ActiveWeaponIndex = activeWeaponIndex;
                }
                PrimaryWeapon.SetStateFromReplication(primaryAmmo, isReloading, reloadRemaining);
                Turret.SetStateFromReplication(turretGunnerPeerId, turretAimRotation, autoTargetShipId);
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

        if (Subsystems == null)
        {
            Subsystems = new ShipSubsystems();
            Subsystems.SubsystemsChanged += OnSubsystemsChanged;
        }

        Turret ??= new ShipTurret();

        _collisionShape = new CollisionShape2D
        {
            Shape = new CircleShape2D { Radius = MovementProfile.CollisionRadius }
        };
        AddChild(_collisionShape);

        _overlayContainer = new Node2D { Name = "OverlayContainer" };
        AddChild(_overlayContainer);

        _infoLabel = new Label
        {
            Position = new Vector2(-110.0f, -58.0f),
            Size = new Vector2(220.0f, 26.0f),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _overlayContainer.AddChild(_infoLabel);

        _camera = new Camera2D
        {
            Enabled = IsLocalPilot() || IsLocalTurretGunner()
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

        if (Subsystems != null)
        {
            Subsystems.SubsystemsChanged -= OnSubsystemsChanged;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Multiplayer.HasMultiplayerPeer() || Multiplayer.MultiplayerPeer is OfflineMultiplayerPeer)
        {
            return;
        }

        float dt = (float)delta;

        bool togglePressed = Input.IsPhysicalKeyPressed(Key.T);
        bool toggleJustPressed = togglePressed && !_prevToggleKey;
        _prevToggleKey = togglePressed;

        if (toggleJustPressed && (IsLocalPilot() || IsLocalTurretGunner()))
        {
            if (Multiplayer.IsServer())
            {
                RequestToggleStationRoleRpc();
            }
            else
            {
                RpcId(1, MethodName.RequestToggleStationRoleRpc);
            }
        }

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
                RpcId(1, MethodName.SubmitPilotCombatInputRpc,
                    input.Throttle,
                    input.Turn,
                    input.Brake,
                    input.Boost,
                    input.FirePrimary,
                    input.ReloadRequested,
                    input.SelectedWeaponSlot,
                    (int)input.TargetSubsystem,
                    input.Tick);
            }
        }
        else if (_testInputOverride.HasValue && Multiplayer.IsServer())
        {
            _localInputTick++;
            ShipControlInput testInput = _testInputOverride.Value;
            testInput.Tick = _localInputTick;
            _currentServerInput = testInput.Sanitized();
            LastProcessedInputTick = testInput.Tick;

            if (testInput.SelectedWeaponSlot >= 0 && testInput.SelectedWeaponSlot < _weapons.Length)
            {
                ActiveWeaponIndex = testInput.SelectedWeaponSlot;
            }

            if (testInput.TargetSubsystem != ShipSubsystemType.None)
            {
                SelectedTargetSubsystem = testInput.TargetSubsystem;
            }

            if (testInput.ReloadRequested)
            {
                PrimaryWeapon.StartReload();
            }
        }

        if (IsLocalTurretGunner())
        {
            TurretControlInput turretInput = _testTurretInputOverride ?? ReadTurretKeyboardMouseInput();
            _localTurretTick++;
            turretInput.Tick = _localTurretTick;

            if (Multiplayer.IsServer())
            {
                ApplyServerTurretInput(Turret.GunnerPeerId, turretInput);
            }
            else
            {
                RpcId(1, MethodName.SubmitTurretInputRpc,
                    turretInput.AimAngleRadians,
                    turretInput.FireTurret,
                    (int)turretInput.TargetSubsystem,
                    turretInput.Tick);
            }
        }
        else if (_testTurretInputOverride.HasValue && Turret.IsManuallyOperated && Multiplayer.IsServer())
        {
            _localTurretTick++;
            TurretControlInput ti = _testTurretInputOverride.Value;
            ti.Tick = _localTurretTick;
            _currentTurretInput = ti.Sanitized();
            LastProcessedTurretTick = ti.Tick;
        }

        if (Multiplayer.IsServer())
        {
            for (int i = 0; i < _weapons.Length; i++)
            {
                _weapons[i].StepCooldown(dt);
            }

            float rot = Rotation;
            Vector2 vel = Velocity;
            Movement.StepPhysics(_currentServerInput, Hull.IsDisabled, dt, ref rot, ref vel, Subsystems.EngineEfficiency);
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

            // Primary weapon firing
            bool primaryDisabled = Hull.IsDisabled || Subsystems.IsWeaponsDisabled;
            if (PrimaryWeapon.TryFire(_currentServerInput.FirePrimary, primaryDisabled, GlobalPosition, Rotation, out Vector2 muzzlePos, out Vector2 fireDir) && _gameWorld != null)
            {
                _gameWorld.SpawnProjectileServer(
                    ShipId,
                    OwnerTeamId,
                    muzzlePos,
                    fireDir,
                    PrimaryWeapon.ProjectileSpeed,
                    PrimaryWeapon.ProjectileDamage,
                    PrimaryWeapon.ProjectileLifetime,
                    PrimaryWeapon.BoltColor,
                    PrimaryWeapon.SubsystemDamage,
                    (int)SelectedTargetSubsystem);
            }

            // Turret stepping & firing
            bool turretDisabled = Hull.IsDisabled || Subsystems.IsTurretDisabled;
            if (Turret.IsManuallyOperated)
            {
                if (Turret.StepManualServer(dt, turretDisabled, GlobalPosition, Rotation, _currentTurretInput.AimAngleRadians, _currentTurretInput.FireTurret, out Vector2 tMuzzle, out Vector2 tDir) && _gameWorld != null)
                {
                    _gameWorld.SpawnProjectileServer(
                        ShipId,
                        OwnerTeamId,
                        tMuzzle,
                        tDir,
                        Turret.Weapon.ProjectileSpeed,
                        Turret.Weapon.ProjectileDamage,
                        Turret.Weapon.ProjectileLifetime,
                        Turret.Weapon.BoltColor,
                        Turret.Weapon.SubsystemDamage,
                        (int)_currentTurretInput.TargetSubsystem);
                }
            }
            else // Turret.IsAutomatedMode
            {
                Ship? nearestEnemy = _gameWorld?.FindNearestEnemyShip(ShipId, OwnerTeamId, GlobalPosition, ShipTurret.AutoAcquireRange);
                if (Turret.StepAutomatedServer(dt, turretDisabled, GlobalPosition, Rotation, nearestEnemy, out Vector2 tMuzzle, out Vector2 tDir) && _gameWorld != null)
                {
                    _gameWorld.SpawnProjectileServer(
                        ShipId,
                        OwnerTeamId,
                        tMuzzle,
                        tDir,
                        Turret.Weapon.ProjectileSpeed,
                        Turret.Weapon.ProjectileDamage,
                        Turret.Weapon.ProjectileLifetime,
                        Turret.Weapon.BoltColor,
                        Turret.Weapon.SubsystemDamage,
                        (int)ShipSubsystemType.Engines);
                }
            }

            Rpc(MethodName.SyncShipStateRpc,
                _authoritativePosition,
                _authoritativeVelocity,
                _authoritativeRotation,
                Movement.IsBoosting,
                Movement.BoostCooldownRemaining,
                LastProcessedInputTick,
                ActiveWeaponIndex,
                PrimaryWeapon.CurrentAmmo,
                PrimaryWeapon.IsReloading,
                PrimaryWeapon.ReloadTimeRemaining,
                Turret.GunnerPeerId,
                Turret.WorldAimRotation,
                Turret.CurrentAutoTargetShipId);
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
        DrawLine(new Vector2(26.0f, 0.0f), new Vector2(38.0f, 0.0f), disabled ? Colors.DarkGray : PrimaryWeapon.BoltColor, 3.0f);

        // Dorsal Turret Mount & Rotating Barrel
        Vector2 mountPos = Turret.LocalMountOffset;
        DrawCircle(mountPos, 6.0f, new Color(0.12f, 0.15f, 0.2f));
        DrawArc(mountPos, 6.0f, 0f, Mathf.Tau, 16, Colors.Gray, 1.0f);

        float localAimAngle = Turret.WorldAimRotation - Rotation;
        Vector2 barrelDir = Vector2.Right.Rotated(localAimAngle);
        Color turretColor = (Subsystems.IsTurretDisabled || disabled)
            ? Colors.DimGray
            : (Turret.IsManuallyOperated ? Colors.Orange : Colors.Cyan);
        DrawLine(mountPos, mountPos + barrelDir * 14.0f, turretColor, 3.0f);
        DrawCircle(mountPos, 3.5f, turretColor);

        // Engine thruster glow
        bool isBoosting = Movement?.IsBoosting ?? false;
        bool isThrusting = (_currentServerInput.Throttle > 0.05f || isBoosting) && Subsystems.EngineEfficiency > 0.0f;
        if (!disabled && isThrusting)
        {
            Color flameColor = isBoosting ? new Color(1.0f, 0.8f, 0.1f) : new Color(0.3f, 0.7f, 1.0f);
            float flameLength = (isBoosting ? 26.0f : 14.0f) * Subsystems.EngineEfficiency;
            _thrusterFlamePolygon[1] = new Vector2(-20.0f - flameLength, 0.0f);
            DrawColoredPolygon(_thrusterFlamePolygon, flameColor);
        }

        // Hull integrity bar
        if (Hull != null)
        {
            float barWidth = 48.0f;
            float barHeight = 4.0f;
            Vector2 barPos = new Vector2(-barWidth / 2.0f, 28.0f);
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
        if (_infoLabel == null || Hull == null || Subsystems == null)
        {
            return;
        }

        string status = Hull.IsDisabled ? " [DISABLED]" : string.Empty;
        string ammoInfo = PrimaryWeapon.IsReloading ? "RELOAD" : $"{PrimaryWeapon.CurrentAmmo}/{PrimaryWeapon.MaxAmmo}";
        string turretInfo = Turret.IsManuallyOperated ? $"[Gunner:{Turret.GunnerPeerId}]" : "[AutoTurret]";
        _infoLabel.Text = $"{ShipName} ({(int)Hull.CurrentHull}/{(int)Hull.MaxHull}){status}\n[{PrimaryWeapon.WeaponName}: {ammoInfo}] {turretInfo}";
    }

    private void OnHullChanged(float current, float max, bool disabled)
    {
        UpdateOverlayText();
        QueueRedraw();
    }

    private void OnSubsystemsChanged()
    {
        UpdateOverlayText();
        QueueRedraw();
    }

    private ShipControlInput ReadPilotKeyboardInput()
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
            || Input.IsMouseButtonPressed(MouseButton.Left);

        bool reloadRequested = Input.IsPhysicalKeyPressed(Key.R);

        int slot = -1;
        if (Input.IsPhysicalKeyPressed(Key.Key1)) slot = 0;
        else if (Input.IsPhysicalKeyPressed(Key.Key2)) slot = 1;
        else if (Input.IsPhysicalKeyPressed(Key.Key3)) slot = 2;

        return new ShipControlInput
        {
            Throttle = throttle,
            Turn = turn,
            Brake = brake,
            Boost = boost,
            FirePrimary = firePrimary,
            ReloadRequested = reloadRequested,
            SelectedWeaponSlot = slot,
            TargetSubsystem = ShipSubsystemType.None
        };
    }

    private TurretControlInput ReadTurretKeyboardMouseInput()
    {
        Vector2 mouseWorld = GetGlobalMousePosition();
        Vector2 mountPos = Turret.GetMountWorldPosition(GlobalPosition, Rotation);
        float aimAngle = (mouseWorld - mountPos).Angle();

        bool fire = Input.IsMouseButtonPressed(MouseButton.Left) || Input.IsPhysicalKeyPressed(Key.Space);

        return new TurretControlInput
        {
            AimAngleRadians = aimAngle,
            FireTurret = fire,
            TargetSubsystem = ShipSubsystemType.None
        };
    }
}
