using System;
using System.Collections.Generic;
using Godot;
using SpaceExtraction.Interiors;

namespace SpaceExtraction.Ships;

/// <summary>
/// Authoritative damage control simulation component (Milestone 4, GAME_DESIGN.md Sections 13-14).
/// Simulates compartment fires, controlled fire spread across open bulkhead doors,
/// firefighting, finite repair supplies, and physical subsystem repair.
/// </summary>
public sealed class ShipDamageControl
{
    public const float MaxFireIntensity = 100.0f;
    public const float FireGrowthPerSecond = 4.0f;
    public const float FireSpreadThreshold = 40.0f;
    public const float FireSpreadIntervalOpenDoor = 4.5f;
    public const float ExtinguishAmountPerAction = 50.0f;
    public const float RepairAmountPerAction = 35.0f;
    public const float RepairSupplyCostPerAction = 10.0f;
    public const float MaxRepairSupplies = 100.0f;
    public const float RepairCooldownSeconds = 0.6f;

    private readonly Dictionary<InteriorRoomType, float> _fireIntensity = new()
    {
        [InteriorRoomType.Cockpit] = 0f,
        [InteriorRoomType.CentralHub] = 0f,
        [InteriorRoomType.EngineRoom] = 0f,
        [InteriorRoomType.Cargo] = 0f,
        [InteriorRoomType.Medical] = 0f
    };

    private readonly Dictionary<InteriorRoomType, float> _spreadTimers = new()
    {
        [InteriorRoomType.Cockpit] = 0f,
        [InteriorRoomType.CentralHub] = 0f,
        [InteriorRoomType.EngineRoom] = 0f,
        [InteriorRoomType.Cargo] = 0f,
        [InteriorRoomType.Medical] = 0f
    };

    private readonly List<InteriorRoomType> _burningRoomsCache = new(5);
    private readonly List<InteriorRoomType> _roomsToIgniteCache = new(5);

    public float MaxSupplies { get; private set; } = MaxRepairSupplies;
    public float CurrentRepairSupplies { get; private set; } = MaxRepairSupplies;
    public float FireHazardMultiplier { get; set; } = 1.0f;
    public float RepairCooldownRemaining { get; private set; }
    public event Action? DamageControlStateChanged;

    public void ConfigureServerSettings(float startingRepairSupplies, float fireHazardMultiplier)
    {
        MaxSupplies = Mathf.Max(0f, startingRepairSupplies);
        CurrentRepairSupplies = MaxSupplies;
        FireHazardMultiplier = Mathf.Max(0f, fireHazardMultiplier);
        DamageControlStateChanged?.Invoke();
    }

    public float GetRoomFireIntensity(InteriorRoomType room)
    {
        return _fireIntensity.TryGetValue(room, out float intensity) ? intensity : 0f;
    }

    public bool IsRoomOnFire(InteriorRoomType room) => GetRoomFireIntensity(room) > 0.01f;

    public bool IsAnyRoomOnFire
    {
        get
        {
            foreach (float intensity in _fireIntensity.Values)
            {
                if (intensity > 0.01f)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public int BurningRoomCount
    {
        get
        {
            int count = 0;
            foreach (float intensity in _fireIntensity.Values)
            {
                if (intensity > 0.01f)
                {
                    count++;
                }
            }
            return count;
        }
    }

    public static InteriorRoomType GetRoomForSubsystem(ShipSubsystemType subsystem)
    {
        return subsystem switch
        {
            ShipSubsystemType.Engines => InteriorRoomType.EngineRoom,
            ShipSubsystemType.Weapons => InteriorRoomType.Cockpit,
            ShipSubsystemType.Turret => InteriorRoomType.CentralHub,
            _ => InteriorRoomType.None
        };
    }

    public static ShipSubsystemType GetSubsystemForRoom(InteriorRoomType room)
    {
        return room switch
        {
            InteriorRoomType.EngineRoom => ShipSubsystemType.Engines,
            InteriorRoomType.Cockpit => ShipSubsystemType.Weapons,
            InteriorRoomType.CentralHub => ShipSubsystemType.Turret,
            _ => ShipSubsystemType.None
        };
    }

    public bool IgniteRoom(InteriorRoomType room, float addedIntensity = 45.0f)
    {
        if (room == InteriorRoomType.None || !_fireIntensity.ContainsKey(room) || FireHazardMultiplier <= 0.001f)
        {
            return false;
        }

        float scaledIntensity = addedIntensity * Mathf.Clamp(FireHazardMultiplier, 0.5f, 2.0f);
        float current = _fireIntensity[room];
        float next = Mathf.Clamp(current + scaledIntensity, 0f, MaxFireIntensity);
        _fireIntensity[room] = next;
        DamageControlStateChanged?.Invoke();
        return true;
    }

    public bool ExtinguishRoom(InteriorRoomType room, float amount = ExtinguishAmountPerAction)
    {
        if (room == InteriorRoomType.None || !_fireIntensity.TryGetValue(room, out float current) || current <= 0.01f)
        {
            return false;
        }

        float next = Mathf.Clamp(current - amount, 0f, MaxFireIntensity);
        _fireIntensity[room] = next;
        if (next <= 0.01f)
        {
            _fireIntensity[room] = 0f;
            _spreadTimers[room] = 0f;
        }

        DamageControlStateChanged?.Invoke();
        return true;
    }

    public bool TryRepairRoomSubsystem(InteriorRoomType room, ShipSubsystems subsystems, bool ignoreCooldown = false)
    {
        // Limitation 1 (Fire Blocks Repair): If room is on fire, repair is blocked
        if (IsRoomOnFire(room))
        {
            return false;
        }

        // Limitation 2 (Repair Cooldown & Finite Supplies)
        if ((!ignoreCooldown && RepairCooldownRemaining > 0.0f) || CurrentRepairSupplies < RepairSupplyCostPerAction)
        {
            return false;
        }

        ShipSubsystemType sys = GetSubsystemForRoom(room);
        if (sys == ShipSubsystemType.None)
        {
            return false;
        }

        bool needsRepair = sys switch
        {
            ShipSubsystemType.Engines => subsystems.CurrentEngineHealth < subsystems.MaxEngineHealth,
            ShipSubsystemType.Weapons => subsystems.CurrentWeaponsHealth < subsystems.MaxWeaponsHealth,
            ShipSubsystemType.Turret => subsystems.CurrentTurretHealth < subsystems.MaxTurretHealth,
            _ => false
        };

        if (!needsRepair)
        {
            return false;
        }

        CurrentRepairSupplies = Mathf.Clamp(CurrentRepairSupplies - RepairSupplyCostPerAction, 0f, MaxSupplies);
        RepairCooldownRemaining = RepairCooldownSeconds;
        subsystems.RepairSubsystem(sys, RepairAmountPerAction);
        DamageControlStateChanged?.Invoke();
        return true;
    }

    public bool StepServer(
        float delta,
        ShipInterior? interior,
        ShipSubsystems subsystems,
        out bool subsystemsDamagedByFire,
        out bool subsystemJustDisabledByFire)
    {
        subsystemsDamagedByFire = false;
        subsystemJustDisabledByFire = false;
        bool discreteFireSpreadOccurred = false;

        if (RepairCooldownRemaining > 0.0f)
        {
            RepairCooldownRemaining = Mathf.Max(0f, RepairCooldownRemaining - delta);
        }

        // Check burning rooms using preallocated cache
        _burningRoomsCache.Clear();
        foreach (var kvp in _fireIntensity)
        {
            if (kvp.Value > 0.01f)
            {
                _burningRoomsCache.Add(kvp.Key);
            }
        }

        if (_burningRoomsCache.Count == 0)
        {
            return false;
        }

        // Rooms to ignite from spread using preallocated cache
        _roomsToIgniteCache.Clear();

        for (int i = 0; i < _burningRoomsCache.Count; i++)
        {
            InteriorRoomType room = _burningRoomsCache[i];
            float intensity = _fireIntensity[room];

            // 1. Fire Growth
            float newIntensity = Mathf.Min(MaxFireIntensity, intensity + FireGrowthPerSecond * FireHazardMultiplier * delta);
            if (Mathf.Abs(newIntensity - intensity) > 0.001f)
            {
                _fireIntensity[room] = newIntensity;
                intensity = newIntensity;
            }

            // 2. Fire attrition damage to room subsystem
            ShipSubsystemType sys = GetSubsystemForRoom(room);
            if (sys != ShipSubsystemType.None)
            {
                float currentHealth = sys switch
                {
                    ShipSubsystemType.Engines => subsystems.CurrentEngineHealth,
                    ShipSubsystemType.Weapons => subsystems.CurrentWeaponsHealth,
                    ShipSubsystemType.Turret => subsystems.CurrentTurretHealth,
                    _ => 0f
                };

                if (currentHealth > 0.0f)
                {
                    float fireDmg = 4.0f * (intensity / 50.0f) * FireHazardMultiplier * delta;
                    subsystems.ApplySubsystemDamage(sys, fireDmg);
                    subsystemsDamagedByFire = true;

                    float afterHealth = sys switch
                    {
                        ShipSubsystemType.Engines => subsystems.CurrentEngineHealth,
                        ShipSubsystemType.Weapons => subsystems.CurrentWeaponsHealth,
                        ShipSubsystemType.Turret => subsystems.CurrentTurretHealth,
                        _ => 0f
                    };

                    if (afterHealth <= 0.0f)
                    {
                        subsystemJustDisabledByFire = true;
                    }
                }
            }

            // 3. Controlled Fire Spread
            if (intensity >= FireSpreadThreshold)
            {
                float spreadTimer = _spreadTimers[room] + delta * FireHazardMultiplier;
                if (spreadTimer >= FireSpreadIntervalOpenDoor)
                {
                    _spreadTimers[room] = 0f;
                    // Check connected doors in interior
                    if (interior != null)
                    {
                        for (int d = 0; d < interior.Doors.Count; d++)
                        {
                            InteriorDoor door = interior.Doors[d];
                            if (door.IsOpen && (door.RoomA == room || door.RoomB == room))
                            {
                                InteriorRoomType adjacent = door.RoomA == room ? door.RoomB : door.RoomA;
                                if (adjacent != InteriorRoomType.None && !IsRoomOnFire(adjacent) && !_roomsToIgniteCache.Contains(adjacent))
                                {
                                    _roomsToIgniteCache.Add(adjacent);
                                }
                            }
                        }
                    }
                }
                else
                {
                    _spreadTimers[room] = spreadTimer;
                }
            }
        }

        for (int i = 0; i < _roomsToIgniteCache.Count; i++)
        {
            InteriorRoomType toIgnite = _roomsToIgniteCache[i];
            IgniteRoom(toIgnite, 35.0f);
            discreteFireSpreadOccurred = true;
        }

        return discreteFireSpreadOccurred;
    }

    public void SetStateFromReplication(
        float cockpitFire,
        float hubFire,
        float engineFire,
        float cargoFire,
        float medicalFire,
        float repairSupplies)
    {
        _fireIntensity[InteriorRoomType.Cockpit] = Mathf.Clamp(cockpitFire, 0f, MaxFireIntensity);
        _fireIntensity[InteriorRoomType.CentralHub] = Mathf.Clamp(hubFire, 0f, MaxFireIntensity);
        _fireIntensity[InteriorRoomType.EngineRoom] = Mathf.Clamp(engineFire, 0f, MaxFireIntensity);
        _fireIntensity[InteriorRoomType.Cargo] = Mathf.Clamp(cargoFire, 0f, MaxFireIntensity);
        _fireIntensity[InteriorRoomType.Medical] = Mathf.Clamp(medicalFire, 0f, MaxFireIntensity);
        CurrentRepairSupplies = Mathf.Clamp(repairSupplies, 0f, MaxSupplies);
        DamageControlStateChanged?.Invoke();
    }
}
