using Godot;

namespace SpaceExtraction.Match;

/// <summary>
/// Authoritative match and combat balance configuration model.
/// Allows tweaking ship durability, subsystem health, weapon damage, automated turret range,
/// flight speeds, repair supplies, fire hazards, and NPC drone settings.
/// </summary>
public sealed class ServerConfig
{
    public const float DefaultCorvetteHullHp = 720.0f;
    public const float DefaultSubsystemHealthMultiplier = 1.0f;
    public const float DefaultTurretAutoRange = 1100.0f;
    public const float DefaultWeaponDamageMultiplier = 1.0f;
    public const float DefaultShipSpeedMultiplier = 1.0f;
    public const float DefaultStartingRepairSupplies = 100.0f;
    public const float DefaultFireHazardMultiplier = 1.0f;

    public float CorvetteHullHp { get; set; } = DefaultCorvetteHullHp;
    public float SubsystemHealthMultiplier { get; set; } = DefaultSubsystemHealthMultiplier;
    public float TurretAutoRange { get; set; } = DefaultTurretAutoRange;
    public float WeaponDamageMultiplier { get; set; } = DefaultWeaponDamageMultiplier;
    public float ShipSpeedMultiplier { get; set; } = DefaultShipSpeedMultiplier;
    public float StartingRepairSupplies { get; set; } = DefaultStartingRepairSupplies;
    public float FireHazardMultiplier { get; set; } = DefaultFireHazardMultiplier;
    public bool TargetDroneTurretActive { get; set; } = false;
    public bool SpawnTargetDrone { get; set; } = true;

    public void ResetToDefaults()
    {
        CorvetteHullHp = DefaultCorvetteHullHp;
        SubsystemHealthMultiplier = DefaultSubsystemHealthMultiplier;
        TurretAutoRange = DefaultTurretAutoRange;
        WeaponDamageMultiplier = DefaultWeaponDamageMultiplier;
        ShipSpeedMultiplier = DefaultShipSpeedMultiplier;
        StartingRepairSupplies = DefaultStartingRepairSupplies;
        FireHazardMultiplier = DefaultFireHazardMultiplier;
        TargetDroneTurretActive = false;
        SpawnTargetDrone = true;
    }

    public ServerConfig CloneSanitized()
    {
        return new ServerConfig
        {
            CorvetteHullHp = Mathf.Clamp(CorvetteHullHp, 100.0f, 5000.0f),
            SubsystemHealthMultiplier = Mathf.Clamp(SubsystemHealthMultiplier, 0.25f, 5.0f),
            TurretAutoRange = Mathf.Clamp(TurretAutoRange, 300.0f, 2500.0f),
            WeaponDamageMultiplier = Mathf.Clamp(WeaponDamageMultiplier, 0.25f, 5.0f),
            ShipSpeedMultiplier = Mathf.Clamp(ShipSpeedMultiplier, 0.5f, 3.0f),
            StartingRepairSupplies = Mathf.Clamp(StartingRepairSupplies, 0.0f, 500.0f),
            FireHazardMultiplier = Mathf.Clamp(FireHazardMultiplier, 0.0f, 3.0f),
            TargetDroneTurretActive = TargetDroneTurretActive,
            SpawnTargetDrone = SpawnTargetDrone
        };
    }
}
