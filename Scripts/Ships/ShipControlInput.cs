using Godot;

namespace SpaceExtraction.Ships;

/// <summary>
/// Represents a single tick of pilot flight and weapon control inputs sent to the authoritative server.
/// </summary>
public struct ShipControlInput
{
    public float Throttle { get; set; }
    public float Turn { get; set; }
    public bool Brake { get; set; }
    public bool Boost { get; set; }
    public bool FirePrimary { get; set; }
    public bool ReloadRequested { get; set; }
    public int SelectedWeaponSlot { get; set; }
    public ShipSubsystemType TargetSubsystem { get; set; }
    public uint Tick { get; set; }

    public ShipControlInput Sanitized()
    {
        float throttle = float.IsFinite(Throttle) ? Mathf.Clamp(Throttle, -1.0f, 1.0f) : 0.0f;
        float turn = float.IsFinite(Turn) ? Mathf.Clamp(Turn, -1.0f, 1.0f) : 0.0f;

        if (Mathf.Abs(throttle) < 0.001f)
        {
            throttle = 0.0f;
        }

        if (Mathf.Abs(turn) < 0.001f)
        {
            turn = 0.0f;
        }

        int weaponSlot = SelectedWeaponSlot is >= 0 and <= 2 ? SelectedWeaponSlot : -1;
        ShipSubsystemType subsystem = TargetSubsystem is ShipSubsystemType.Engines or ShipSubsystemType.Weapons or ShipSubsystemType.Turret
            ? TargetSubsystem
            : ShipSubsystemType.Engines;

        return new ShipControlInput
        {
            Throttle = throttle,
            Turn = turn,
            Brake = brakeOrDefault(Brake),
            Boost = Boost,
            FirePrimary = FirePrimary,
            ReloadRequested = ReloadRequested,
            SelectedWeaponSlot = weaponSlot,
            TargetSubsystem = subsystem,
            Tick = Tick
        };

        static bool brakeOrDefault(bool b) => b;
    }
}

/// <summary>
/// Represents a single tick of manual turret operator input sent to the authoritative server.
/// </summary>
public struct TurretControlInput
{
    public float AimAngleRadians { get; set; }
    public bool FireTurret { get; set; }
    public ShipSubsystemType TargetSubsystem { get; set; }
    public uint Tick { get; set; }

    public TurretControlInput Sanitized()
    {
        float angle = float.IsFinite(AimAngleRadians)
            ? Mathf.Wrap(AimAngleRadians, -Mathf.Pi, Mathf.Pi)
            : 0.0f;

        ShipSubsystemType subsystem = TargetSubsystem is ShipSubsystemType.Engines or ShipSubsystemType.Weapons or ShipSubsystemType.Turret
            ? TargetSubsystem
            : ShipSubsystemType.Engines;

        return new TurretControlInput
        {
            AimAngleRadians = angle,
            FireTurret = FireTurret,
            TargetSubsystem = subsystem,
            Tick = Tick
        };
    }
}
