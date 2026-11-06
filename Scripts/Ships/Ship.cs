using System;
using System.Collections.Generic;
using Godot;
using SpaceExtraction.Characters;
using SpaceExtraction.Interiors;
using SpaceExtraction.Match;
using SpaceExtraction.World;

namespace SpaceExtraction.Ships;

/// <summary>
/// Server-authoritative ship entity (Milestones 1, 2 & 3).
/// Supports physics simulation, weapon switching/reloading, turret operations (manual & automated),
/// pilot/gunner station role toggles, discrete subsystem damage tracking,
/// physical interior walking simulation, bulkhead doors, and console interactions.
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
    public ShipTurret Turret { get; private set; } = new() { LocalMountOffset = new Vector2(-24.0f, 0.0f) };
    public ShipInterior Interior { get; private set; } = ShipInterior.CreateCorvetteInterior();
    public ShipDamageControl DamageControl { get; private set; } = new();
    public float ExteriorRoofAlpha { get; private set; } = 1.0f;

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
    private float _damageControlSyncAccumulator;
    private float _subsystemFireSyncAccumulator;
    private bool _npcTurretActive;

    private readonly HashSet<long> _occupantPeerIds = new();

    private CollisionShape2D? _collisionShape;
    private Node2D? _overlayContainer;
    private Label? _infoLabel;
    private Camera2D? _camera;

    public const float MinCameraZoom = 0.35f;
    public const float MaxCameraZoom = 2.75f;
    public const float CameraZoomStep = 0.12f;
    private float _pilotTargetZoom = 0.85f;
    private float _onFootTargetZoom = 1.25f;

    private static readonly Vector2[] BodyPolygon =
    {
        new(184.0f, 0.0f),
        new(156.0f, -52.0f),
        new(60.0f, -56.0f),
        new(56.0f, -120.0f),
        new(-56.0f, -120.0f),
        new(-60.0f, -64.0f),
        new(-164.0f, -64.0f),
        new(-172.0f, 0.0f),
        new(-164.0f, 64.0f),
        new(-60.0f, 64.0f),
        new(-56.0f, 120.0f),
        new(56.0f, 120.0f),
        new(60.0f, 56.0f),
        new(156.0f, 52.0f)
    };

    private static readonly Vector2[] ExteriorCanopyPolygon =
    {
        new(104.0f, -26.0f),
        new(154.0f, -14.0f),
        new(154.0f, 14.0f),
        new(104.0f, 26.0f)
    };

    private readonly Vector2[] _thrusterFlamePolygon =
    {
        new(-168.0f, -18.0f),
        new(-204.0f, 0.0f),
        new(-168.0f, 18.0f)
    };

    public Ship()
    {
        Movement = new ShipMovement(MovementProfile);
        Hull = new ShipHull(MovementProfile.MaxHullIntegrity);
        Subsystems = new ShipSubsystems();
        Turret = new ShipTurret { LocalMountOffset = new Vector2(-24.0f, 0.0f) };
        Interior = ShipInterior.CreateCorvetteInterior();
        DamageControl = new ShipDamageControl();
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

        if (DamageControl != null)
        {
            DamageControl.DamageControlStateChanged -= OnDamageControlChanged;
        }

        MovementProfile = ShipMovementProfile.FromClass(shipClass);
        Movement = new ShipMovement(MovementProfile);
        Hull = new ShipHull(MovementProfile.MaxHullIntegrity);
        Hull.HullChanged += OnHullChanged;

        Subsystems = new ShipSubsystems();
        Subsystems.SubsystemsChanged += OnSubsystemsChanged;

        DamageControl = new ShipDamageControl();
        DamageControl.DamageControlStateChanged += OnDamageControlChanged;

        _weapons = new[]
        {
            ShipWeapon.CreateAutocannon(),
            ShipWeapon.CreateRailgun(),
            ShipWeapon.CreateEmpCannon()
        };
        ActiveWeaponIndex = 0;
        SelectedTargetSubsystem = ShipSubsystemType.Engines;

        Turret = new ShipTurret { LocalMountOffset = new Vector2(-24.0f, 0.0f) };
        Interior = ShipInterior.CreateCorvetteInterior();

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

    public void ApplyServerConfig(ServerConfig config, bool resetHealthAndSupplies = true)
    {
        float hullScale = config.CorvetteHullHp / ServerConfig.DefaultCorvetteHullHp;
        MovementProfile = ShipMovementProfile.FromClass(ShipClass).WithServerMultipliers(hullScale, config.ShipSpeedMultiplier);
        Movement = new ShipMovement(MovementProfile);

        if (resetHealthAndSupplies)
        {
            Hull.SetStateFromReplication(MovementProfile.MaxHullIntegrity, MovementProfile.MaxHullIntegrity, false);
            Subsystems.ApplyMaxHealthMultiplier(config.SubsystemHealthMultiplier, true);
            DamageControl.ConfigureServerSettings(config.StartingRepairSupplies, config.FireHazardMultiplier);
        }
        else
        {
            Subsystems.ApplyMaxHealthMultiplier(config.SubsystemHealthMultiplier, false);
        }

        Turret.AutoAcquireRange = config.TurretAutoRange;
        for (int i = 0; i < _weapons.Length; i++)
        {
            _weapons[i].DamageMultiplier = config.WeaponDamageMultiplier;
        }
        Turret.Weapon.DamageMultiplier = config.WeaponDamageMultiplier;
        _npcTurretActive = config.TargetDroneTurretActive;

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
            _camera.Enabled = IsLocalOccupant();
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
            _camera.Enabled = IsLocalOccupant();
        }
    }

    public void AddOccupant(long peerId)
    {
        if (peerId > 0)
        {
            _occupantPeerIds.Add(peerId);
        }

        if (_camera != null)
        {
            _camera.Enabled = IsLocalOccupant();
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
            _camera.Enabled = IsLocalOccupant();
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

    public bool IsLocalOccupant()
    {
        if (!Multiplayer.HasMultiplayerPeer() || Multiplayer.MultiplayerPeer is OfflineMultiplayerPeer)
        {
            return false;
        }

        long localId = Multiplayer.GetUniqueId();
        return _occupantPeerIds.Contains(localId) || IsLocalPilot() || IsLocalTurretGunner();
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
            PlayerCharacter? character = _gameWorld?.GetPlayerCharacter(senderId);
            character?.Rpc(PlayerCharacter.MethodName.ForceSyncInteriorPosRpc, new Vector2(0.0f, 0.0f));
        }
        else if (Turret.GunnerPeerId == senderId && PilotPeerId == 0)
        {
            SetStationRolesServer(senderId, 0);
            PlayerCharacter? character = _gameWorld?.GetPlayerCharacter(senderId);
            character?.Rpc(PlayerCharacter.MethodName.ForceSyncInteriorPosRpc, new Vector2(108.0f, 0.0f));
        }
        else if (Turret.GunnerPeerId == 0 && PilotPeerId != senderId)
        {
            SetStationRolesServer(PilotPeerId, senderId);
            PlayerCharacter? character = _gameWorld?.GetPlayerCharacter(senderId);
            character?.Rpc(PlayerCharacter.MethodName.ForceSyncInteriorPosRpc, new Vector2(0.0f, 0.0f));
        }
        else if (PilotPeerId == 0 && Turret.GunnerPeerId != senderId)
        {
            SetStationRolesServer(senderId, Turret.GunnerPeerId);
            PlayerCharacter? character = _gameWorld?.GetPlayerCharacter(senderId);
            character?.Rpc(PlayerCharacter.MethodName.ForceSyncInteriorPosRpc, new Vector2(108.0f, 0.0f));
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

        if (_gameWorld != null && IsInsideTree())
        {
            NodePath myPath = GetPath();
            foreach (long occPeerId in _occupantPeerIds)
            {
                PlayerCharacter? occChar = _gameWorld.GetPlayerCharacter(occPeerId);
                occChar?.SetShipOccupancy(
                    ShipId,
                    PilotPeerId == occPeerId,
                    myPath,
                    Turret.GunnerPeerId == occPeerId);
            }
        }

        if (_camera != null)
        {
            _camera.Enabled = IsLocalOccupant();
        }

        UpdateOverlayText();
        QueueRedraw();
    }

    public void RequestInteractFromLocalPlayer()
    {
        if (Multiplayer.IsServer())
        {
            HandlePlayerInteriorInteractServer(Multiplayer.GetUniqueId());
        }
        else
        {
            RpcId(1, MethodName.RequestInteractInteriorRpc);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestInteractInteriorRpc()
    {
        long senderId = Multiplayer.GetRemoteSenderId();
        if (senderId == 0)
        {
            senderId = Multiplayer.GetUniqueId();
        }
        HandlePlayerInteriorInteractServer(senderId);
    }

    public void HandlePlayerInteriorInteractServer(long peerId)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        PlayerCharacter? character = _gameWorld?.GetPlayerCharacter(peerId);
        if (!_occupantPeerIds.Contains(peerId) &&
            PilotPeerId != peerId &&
            Turret.GunnerPeerId != peerId &&
            (character == null || character.LocationContext.OccupiedShipId != ShipId))
        {
            return;
        }

        _occupantPeerIds.Add(peerId);

        if (PilotPeerId == peerId)
        {
            SetStationRolesServer(0, Turret.GunnerPeerId);
            character?.Rpc(PlayerCharacter.MethodName.ForceSyncInteriorPosRpc, new Vector2(108.0f, 0.0f));
            return;
        }

        if (Turret.GunnerPeerId == peerId)
        {
            SetStationRolesServer(PilotPeerId, 0);
            character?.Rpc(PlayerCharacter.MethodName.ForceSyncInteriorPosRpc, new Vector2(0.0f, 0.0f));
            return;
        }

        // On Foot inside the ship
        if (character == null)
        {
            return;
        }

        InteriorRoomType currentRoom = character.CurrentInteriorRoom;

        // 1. If current room is actively on fire, E key acts as firefighting action!
        if (DamageControl.IsRoomOnFire(currentRoom))
        {
            DamageControl.ExtinguishRoom(currentRoom);
            BroadcastDamageControlStateServer();
            return;
        }

        InteriorConsole? console = Interior.FindNearestInteractableConsole(character.ShipLocalPosition);
        if (console != null)
        {
            Rpc(MethodName.SyncConsoleInteractionRpc, (int)console.ConsoleType, peerId);

            if (console.ConsoleType == InteriorConsoleType.Pilot && PilotPeerId == 0)
            {
                SetStationRolesServer(peerId, Turret.GunnerPeerId);
                character.Rpc(PlayerCharacter.MethodName.ForceSyncInteriorPosRpc, console.SeatOrStandLocalPosition);
            }
            else if (console.ConsoleType == InteriorConsoleType.Turret && Turret.GunnerPeerId == 0)
            {
                if (Subsystems.IsTurretDisabled && DamageControl.TryRepairRoomSubsystem(InteriorRoomType.CentralHub, Subsystems))
                {
                    Rpc(MethodName.SyncSubsystemsStateRpc, Subsystems.CurrentEngineHealth, Subsystems.CurrentWeaponsHealth, Subsystems.CurrentTurretHealth);
                    BroadcastDamageControlStateServer();
                }
                else
                {
                    SetStationRolesServer(PilotPeerId, peerId);
                    character.Rpc(PlayerCharacter.MethodName.ForceSyncInteriorPosRpc, console.SeatOrStandLocalPosition);
                }
            }
            else if (console.ConsoleType == InteriorConsoleType.Engineering)
            {
                if (DamageControl.TryRepairRoomSubsystem(InteriorRoomType.EngineRoom, Subsystems))
                {
                    Rpc(MethodName.SyncSubsystemsStateRpc, Subsystems.CurrentEngineHealth, Subsystems.CurrentWeaponsHealth, Subsystems.CurrentTurretHealth);
                    BroadcastDamageControlStateServer();
                }
            }
        }
        else
        {
            InteriorDoor? door = Interior.FindNearestInteractableDoor(character.ShipLocalPosition);
            if (door != null)
            {
                SetDoorOpenServer(door.DoorId, !door.IsOpen);
            }
        }
    }

    public void RequestRepairOrExtinguishFromLocalPlayer()
    {
        if (Multiplayer.IsServer())
        {
            HandlePlayerRepairOrExtinguishServer(Multiplayer.GetUniqueId());
        }
        else
        {
            RpcId(1, MethodName.RequestRepairOrExtinguishRpc);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestRepairOrExtinguishRpc()
    {
        long senderId = Multiplayer.GetRemoteSenderId();
        if (senderId == 0)
        {
            senderId = Multiplayer.GetUniqueId();
        }
        HandlePlayerRepairOrExtinguishServer(senderId);
    }

    public void HandlePlayerRepairOrExtinguishServer(long peerId, bool ignoreCooldown = false)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        PlayerCharacter? character = _gameWorld?.GetPlayerCharacter(peerId);
        if (!_occupantPeerIds.Contains(peerId) &&
            PilotPeerId != peerId &&
            Turret.GunnerPeerId != peerId &&
            (character == null || character.LocationContext.OccupiedShipId != ShipId))
        {
            return;
        }

        if (character == null)
        {
            return;
        }

        InteriorRoomType currentRoom = character.CurrentInteriorRoom;
        if (DamageControl.IsRoomOnFire(currentRoom))
        {
            DamageControl.ExtinguishRoom(currentRoom);
            BroadcastDamageControlStateServer();
        }
        else
        {
            if (DamageControl.TryRepairRoomSubsystem(currentRoom, Subsystems, ignoreCooldown))
            {
                Rpc(MethodName.SyncSubsystemsStateRpc, Subsystems.CurrentEngineHealth, Subsystems.CurrentWeaponsHealth, Subsystems.CurrentTurretHealth);
                BroadcastDamageControlStateServer();
            }
        }
    }

    public void RequestToggleNearestDoorFromLocalPlayer()
    {
        if (Multiplayer.IsServer())
        {
            HandleToggleNearestDoorServer(Multiplayer.GetUniqueId());
        }
        else
        {
            RpcId(1, MethodName.RequestToggleNearestDoorRpc);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestToggleNearestDoorRpc()
    {
        long senderId = Multiplayer.GetRemoteSenderId();
        if (senderId == 0)
        {
            senderId = Multiplayer.GetUniqueId();
        }
        HandleToggleNearestDoorServer(senderId);
    }

    public void HandleToggleNearestDoorServer(long peerId)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        PlayerCharacter? character = _gameWorld?.GetPlayerCharacter(peerId);
        if (!_occupantPeerIds.Contains(peerId) &&
            PilotPeerId != peerId &&
            Turret.GunnerPeerId != peerId &&
            (character == null || character.LocationContext.OccupiedShipId != ShipId))
        {
            return;
        }

        if (character != null)
        {
            InteriorDoor? door = Interior.FindNearestInteractableDoor(character.ShipLocalPosition);
            if (door != null)
            {
                SetDoorOpenServer(door.DoorId, !door.IsOpen);
            }
        }
    }

    public void SetDoorOpenServer(int doorId, bool isOpen)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        if (Interior.SetDoorOpen(doorId, isOpen))
        {
            Rpc(MethodName.SyncDoorsStateRpc, Interior.GetDoorsBitmask());
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncDoorsStateRpc(uint doorsBitmask)
    {
        Interior.ApplyDoorsBitmask(doorsBitmask);
        QueueRedraw();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncConsoleInteractionRpc(int consoleTypeInt, long peerId)
    {
        Interior.RecordConsoleInteraction((InteriorConsoleType)consoleTypeInt, peerId);
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

    public const float BowSubsystemZoneThresholdX = 52.0f;
    public const float SternSubsystemZoneThresholdX = -52.0f;

    /// <summary>
    /// Resolves which subsystem occupies the impacted hull section in the ship's local coordinate space:
    /// - Bow / Cockpit (local X > +52) -> Weapons
    /// - Midships / Central Hub (-52 <= local X <= +52) -> Turret
    /// - Stern / Engine Room (local X < -52) -> Engines
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

    /// <summary>
    /// Tests whether a swept world-space projectile segment [prevWorldPos, nextWorldPos] intersects
    /// the ship's 2D hull polygon, returning the world-space impact entry point.
    /// </summary>
    public bool TryIntersectProjectileSegment(Vector2 prevWorldPos, Vector2 nextWorldPos, out Vector2 worldImpactPoint)
    {
        worldImpactPoint = prevWorldPos;
        Vector2 closestWorld = Geometry2D.GetClosestPointToSegment(GlobalPosition, prevWorldPos, nextWorldPos);
        if (closestWorld.DistanceSquaredTo(GlobalPosition) > 192.0f * 192.0f)
        {
            return false;
        }

        Vector2 localPrev = (prevWorldPos - GlobalPosition).Rotated(-Rotation);
        Vector2 localNext = (nextWorldPos - GlobalPosition).Rotated(-Rotation);

        const int sampleCount = 8;
        for (int i = 0; i <= sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            Vector2 localSample = localPrev.Lerp(localNext, t);
            if (Geometry2D.IsPointInPolygon(localSample, BodyPolygon))
            {
                worldImpactPoint = GlobalPosition + localSample.Rotated(Rotation);
                return true;
            }
        }

        return false;
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

            if (subsystemDamage >= 20.0f)
            {
                InteriorRoomType hitRoom = ShipDamageControl.GetRoomForSubsystem(targetSubsystem);
                if (hitRoom != InteriorRoomType.None)
                {
                    float currentHealth = targetSubsystem switch
                    {
                        ShipSubsystemType.Engines => Subsystems.CurrentEngineHealth,
                        ShipSubsystemType.Weapons => Subsystems.CurrentWeaponsHealth,
                        ShipSubsystemType.Turret => Subsystems.CurrentTurretHealth,
                        _ => 0f
                    };
                    float maxHealth = targetSubsystem switch
                    {
                        ShipSubsystemType.Engines => Subsystems.MaxEngineHealth,
                        ShipSubsystemType.Weapons => Subsystems.MaxWeaponsHealth,
                        ShipSubsystemType.Turret => Subsystems.MaxTurretHealth,
                        _ => 1f
                    };

                    if (currentHealth <= maxHealth * 0.75f || currentHealth <= 0.0f)
                    {
                        DamageControl.IgniteRoom(hitRoom, 45.0f);
                        BroadcastDamageControlStateServer();
                    }
                }
            }
        }
    }

    public void BroadcastDamageControlStateServer()
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        Rpc(MethodName.SyncDamageControlStateRpc,
            DamageControl.GetRoomFireIntensity(InteriorRoomType.Cockpit),
            DamageControl.GetRoomFireIntensity(InteriorRoomType.CentralHub),
            DamageControl.GetRoomFireIntensity(InteriorRoomType.EngineRoom),
            DamageControl.GetRoomFireIntensity(InteriorRoomType.Cargo),
            DamageControl.GetRoomFireIntensity(InteriorRoomType.Medical),
            DamageControl.CurrentRepairSupplies);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncDamageControlStateRpc(
        float cockpitFire,
        float hubFire,
        float engineFire,
        float cargoFire,
        float medicalFire,
        float repairSupplies)
    {
        DamageControl.SetStateFromReplication(cockpitFire, hubFire, engineFire, cargoFire, medicalFire, repairSupplies);
        UpdateOverlayText();
        QueueRedraw();
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

        Turret ??= new ShipTurret { LocalMountOffset = new Vector2(-24.0f, 0.0f) };
        Interior ??= ShipInterior.CreateCorvetteInterior();
        if (DamageControl == null)
        {
            DamageControl = new ShipDamageControl();
            DamageControl.DamageControlStateChanged += OnDamageControlChanged;
        }

        _collisionShape = new CollisionShape2D
        {
            Shape = new CircleShape2D { Radius = MovementProfile.CollisionRadius }
        };
        AddChild(_collisionShape);

        _overlayContainer = new Node2D { Name = "OverlayContainer" };
        AddChild(_overlayContainer);

        _infoLabel = new Label
        {
            Position = new Vector2(-110.0f, -152.0f),
            Size = new Vector2(220.0f, 26.0f),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _overlayContainer.AddChild(_infoLabel);

        _camera = new Camera2D
        {
            Enabled = IsLocalOccupant()
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

        if (DamageControl != null)
        {
            DamageControl.DamageControlStateChanged -= OnDamageControlChanged;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsLocalOccupant())
        {
            return;
        }

        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            bool inStationControl = IsLocalPilot() || IsLocalTurretGunner();
            if (mb.ButtonIndex == MouseButton.WheelUp)
            {
                if (inStationControl)
                {
                    _pilotTargetZoom = Mathf.Clamp(_pilotTargetZoom + CameraZoomStep, MinCameraZoom, MaxCameraZoom);
                }
                else
                {
                    _onFootTargetZoom = Mathf.Clamp(_onFootTargetZoom + CameraZoomStep, MinCameraZoom, MaxCameraZoom);
                }
            }
            else if (mb.ButtonIndex == MouseButton.WheelDown)
            {
                if (inStationControl)
                {
                    _pilotTargetZoom = Mathf.Clamp(_pilotTargetZoom - CameraZoomStep, MinCameraZoom, MaxCameraZoom);
                }
                else
                {
                    _onFootTargetZoom = Mathf.Clamp(_onFootTargetZoom - CameraZoomStep, MinCameraZoom, MaxCameraZoom);
                }
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Multiplayer.HasMultiplayerPeer() || Multiplayer.MultiplayerPeer is OfflineMultiplayerPeer)
        {
            return;
        }

        float dt = (float)delta;

        bool showInteriorCutaway = IsLocalOccupant() && !IsLocalPilot() && !IsLocalTurretGunner();
        float targetRoofAlpha = showInteriorCutaway ? 0.0f : 1.0f;
        ExteriorRoofAlpha = Mathf.MoveToward(ExteriorRoofAlpha, targetRoofAlpha, dt * 4.5f);

        if (_camera != null && _camera.Enabled)
        {
            float desiredZoom = (IsLocalPilot() || IsLocalTurretGunner()) ? _pilotTargetZoom : _onFootTargetZoom;
            _camera.Zoom = _camera.Zoom.Lerp(new Vector2(desiredZoom, desiredZoom), Mathf.Clamp(dt * 10.0f, 0f, 1f));
        }

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

            bool discreteFireSpread = DamageControl.StepServer(
                dt,
                Interior,
                Subsystems,
                out bool subDamaged,
                out bool subJustDisabled);
            _damageControlSyncAccumulator += dt;

            if (subDamaged)
            {
                _subsystemFireSyncAccumulator += dt;
                if (subJustDisabled || _subsystemFireSyncAccumulator >= 0.25f)
                {
                    _subsystemFireSyncAccumulator = 0f;
                    Rpc(MethodName.SyncSubsystemsStateRpc, Subsystems.CurrentEngineHealth, Subsystems.CurrentWeaponsHealth, Subsystems.CurrentTurretHealth);
                }
            }

            if (discreteFireSpread || (DamageControl.IsAnyRoomOnFire && _damageControlSyncAccumulator >= 0.25f))
            {
                _damageControlSyncAccumulator = 0f;
                BroadcastDamageControlStateServer();
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
                    PrimaryWeapon.EffectiveProjectileDamage,
                    PrimaryWeapon.ProjectileLifetime,
                    PrimaryWeapon.BoltColor,
                    PrimaryWeapon.EffectiveSubsystemDamage,
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
                        Turret.Weapon.EffectiveProjectileDamage,
                        Turret.Weapon.ProjectileLifetime,
                        Turret.Weapon.BoltColor,
                        Turret.Weapon.EffectiveSubsystemDamage,
                        (int)_currentTurretInput.TargetSubsystem);
                }
            }
            else // Turret.IsAutomatedMode
            {
                Ship? nearestEnemy = null;
                if (!IsNpcTarget || _npcTurretActive)
                {
                    Vector2 mountPos = Turret.GetMountWorldPosition(GlobalPosition, Rotation);
                    nearestEnemy = _gameWorld?.FindNearestEnemyShip(ShipId, OwnerTeamId, mountPos, Turret.AutoAcquireRange);
                }

                if (Turret.StepAutomatedServer(dt, turretDisabled, GlobalPosition, Rotation, nearestEnemy, out Vector2 tMuzzle, out Vector2 tDir) && _gameWorld != null)
                {
                    _gameWorld.SpawnProjectileServer(
                        ShipId,
                        OwnerTeamId,
                        tMuzzle,
                        tDir,
                        Turret.Weapon.ProjectileSpeed,
                        Turret.Weapon.EffectiveProjectileDamage,
                        Turret.Weapon.ProjectileLifetime,
                        Turret.Weapon.BoltColor,
                        Turret.Weapon.EffectiveSubsystemDamage,
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

        // Interior Rooms & Stations (Visible when ExteriorRoofAlpha < 0.99f)
        if (Interior != null && ExteriorRoofAlpha < 0.99f)
        {
            for (int i = 0; i < Interior.Rooms.Count; i++)
            {
                InteriorRoom room = Interior.Rooms[i];
                DrawRect(room.Bounds, disabled ? room.FloorColor.Darkened(0.45f) : room.FloorColor, true);

                InteriorConsole? roomConsole = Interior.GetConsoleByType((InteriorConsoleType)(int)room.RoomType);
                Color accentColor = roomConsole != null ? roomConsole.ConsoleColor.Darkened(0.25f) : new Color(0.38f, 0.45f, 0.55f, 0.9f);
                DrawRect(room.Bounds, accentColor, false, 2.0f);

                // Milestone 4: Room fire hazard overlay & flame circles
                if (DamageControl != null && DamageControl.IsRoomOnFire(room.RoomType))
                {
                    float fireFrac = Mathf.Clamp(DamageControl.GetRoomFireIntensity(room.RoomType) / 100.0f, 0.15f, 1.0f);
                    float interiorAlpha = 1.0f - ExteriorRoofAlpha;
                    Color fireOverlay = new Color(1.0f, 0.30f, 0.08f, 0.38f * fireFrac * interiorAlpha);
                    DrawRect(room.Bounds, fireOverlay, true);

                    Vector2 center = room.Bounds.GetCenter();
                    float flameRadius = 8.0f + 4.0f * fireFrac;
                    Vector2 f1 = center + new Vector2(-18.0f, 0.0f);
                    Vector2 f2 = center;
                    Vector2 f3 = center + new Vector2(18.0f, 0.0f);

                    DrawCircle(f1, flameRadius, new Color(1.0f, 0.25f, 0.05f, 0.85f * interiorAlpha));
                    DrawCircle(f1, flameRadius * 0.55f, new Color(1.0f, 0.85f, 0.2f, 0.95f * interiorAlpha));
                    DrawCircle(f2, flameRadius * 1.25f, new Color(1.0f, 0.25f, 0.05f, 0.85f * interiorAlpha));
                    DrawCircle(f2, flameRadius * 0.65f, new Color(1.0f, 0.85f, 0.2f, 0.95f * interiorAlpha));
                    DrawCircle(f3, flameRadius, new Color(1.0f, 0.25f, 0.05f, 0.85f * interiorAlpha));
                    DrawCircle(f3, flameRadius * 0.55f, new Color(1.0f, 0.85f, 0.2f, 0.95f * interiorAlpha));
                }
            }

            // Interior Doors
            for (int i = 0; i < Interior.Doors.Count; i++)
            {
                InteriorDoor door = Interior.Doors[i];
                DrawRect(door.VisualRect, new Color(0.12f, 0.14f, 0.18f, 1.0f), true);
                if (door.IsOpen)
                {
                    DrawRect(door.VisualRect, new Color(0.25f, 0.95f, 0.45f, 0.85f), false, 1.5f);
                }
                else
                {
                    DrawRect(door.VisualRect, new Color(0.95f, 0.3f, 0.2f, 0.95f), true);
                    DrawRect(door.VisualRect, Colors.White, false, 1.5f);
                }
            }

            // Interior Consoles
            for (int i = 0; i < Interior.Consoles.Count; i++)
            {
                InteriorConsole console = Interior.Consoles[i];
                DrawRect(console.VisualRect, disabled ? console.ConsoleColor.Darkened(0.5f) : console.ConsoleColor, true);
                DrawRect(console.VisualRect, Colors.White, false, 1.2f);
            }
        }

        // Exterior Hull Cover & Canopy (Visible when ExteriorRoofAlpha > 0.01f)
        if (ExteriorRoofAlpha > 0.01f)
        {
            Color coverColor = new Color(baseColor.R, baseColor.G, baseColor.B, ExteriorRoofAlpha);
            DrawColoredPolygon(BodyPolygon, coverColor);

            Color seamColor = new Color(0.15f, 0.2f, 0.28f, 0.75f * ExteriorRoofAlpha);
            DrawLine(new Vector2(-156f, -48f), new Vector2(140f, -48f), seamColor, 2.0f);
            DrawLine(new Vector2(-156f, 48f), new Vector2(140f, 48f), seamColor, 2.0f);
            DrawLine(new Vector2(52f, -48f), new Vector2(52f, 48f), seamColor, 2.0f);
            DrawLine(new Vector2(-52f, -48f), new Vector2(-52f, 48f), seamColor, 2.0f);

            Color canopyColor = new Color(0.75f, 0.92f, 1.0f, 0.9f * ExteriorRoofAlpha);
            DrawColoredPolygon(ExteriorCanopyPolygon, canopyColor);
            for (int i = 0; i < ExteriorCanopyPolygon.Length; i++)
            {
                Vector2 c1 = ExteriorCanopyPolygon[i];
                Vector2 c2 = ExteriorCanopyPolygon[(i + 1) % ExteriorCanopyPolygon.Length];
                DrawLine(c1, c2, new Color(Colors.White.R, Colors.White.G, Colors.White.B, ExteriorRoofAlpha), 1.5f);
            }

            Color coverOutlineColor = new Color(outlineColor.R, outlineColor.G, outlineColor.B, outlineColor.A * ExteriorRoofAlpha);
            for (int i = 0; i < BodyPolygon.Length; i++)
            {
                Vector2 p1 = BodyPolygon[i];
                Vector2 p2 = BodyPolygon[(i + 1) % BodyPolygon.Length];
                DrawLine(p1, p2, coverOutlineColor, 1.8f);
            }

            // Milestone 4: Exterior fire warning border glow
            if (DamageControl != null && DamageControl.IsAnyRoomOnFire)
            {
                Color fireGlowColor = new Color(1.0f, 0.35f, 0.05f, 0.45f * ExteriorRoofAlpha);
                for (int i = 0; i < BodyPolygon.Length; i++)
                {
                    Vector2 p1 = BodyPolygon[i];
                    Vector2 p2 = BodyPolygon[(i + 1) % BodyPolygon.Length];
                    DrawLine(p1, p2, fireGlowColor, 4.0f);
                }
            }
        }

        // Forward weapon cannon barrel at the bow nose tip
        DrawLine(new Vector2(174.0f, 0.0f), new Vector2(198.0f, 0.0f), disabled ? Colors.DarkGray : PrimaryWeapon.BoltColor, 4.5f);

        // Dorsal Turret Mount & Rotating Barrel
        Vector2 mountPos = Turret.LocalMountOffset;
        DrawCircle(mountPos, 10.0f, new Color(0.12f, 0.15f, 0.2f));
        DrawArc(mountPos, 10.0f, 0f, Mathf.Tau, 20, Colors.Gray, 1.5f);

        float localAimAngle = Turret.WorldAimRotation - Rotation;
        Vector2 barrelDir = Vector2.Right.Rotated(localAimAngle);
        Color turretColor = (Subsystems.IsTurretDisabled || disabled)
            ? Colors.DimGray
            : (Turret.IsManuallyOperated ? Colors.Orange : Colors.Cyan);
        DrawLine(mountPos, mountPos + barrelDir * 28.0f, turretColor, 4.0f);
        DrawCircle(mountPos, 5.0f, turretColor);

        // Engine thruster glow at stern
        bool isBoosting = Movement?.IsBoosting ?? false;
        bool isThrusting = (_currentServerInput.Throttle > 0.05f || isBoosting) && Subsystems.EngineEfficiency > 0.0f;
        if (!disabled && isThrusting)
        {
            Color flameColor = isBoosting ? new Color(1.0f, 0.8f, 0.1f) : new Color(0.3f, 0.7f, 1.0f);
            float flameLength = (isBoosting ? 46.0f : 26.0f) * Subsystems.EngineEfficiency;
            _thrusterFlamePolygon[1] = new Vector2(-168.0f - flameLength, 0.0f);
            DrawColoredPolygon(_thrusterFlamePolygon, flameColor);
        }

        // Hull integrity bar
        if (Hull != null)
        {
            float barWidth = 96.0f;
            float barHeight = 6.0f;
            Vector2 barPos = new Vector2(-barWidth / 2.0f, 130.0f);
            DrawRect(new Rect2(barPos, new Vector2(barWidth, barHeight)), new Color(0f, 0f, 0f, 0.6f), true);
            float fillWidth = barWidth * Hull.HullFraction;
            Color barColor = disabled ? Colors.DarkRed : (Hull.HullFraction > 0.4f ? Colors.LimeGreen : Colors.OrangeRed);
            if (fillWidth > 0f)
            {
                DrawRect(new Rect2(barPos, new Vector2(fillWidth, barHeight)), barColor, true);
            }
            DrawRect(new Rect2(barPos, new Vector2(barWidth, barHeight)), Colors.White, false, 1.2f);
        }
    }

    private void UpdateOverlayText()
    {
        if (_infoLabel == null || Hull == null || Subsystems == null)
        {
            return;
        }

        string status = Hull.IsDisabled ? " [DISABLED]" : string.Empty;
        if (DamageControl != null && DamageControl.IsAnyRoomOnFire)
        {
            status += " [FIRE!]";
        }
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

    private void OnDamageControlChanged()
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
