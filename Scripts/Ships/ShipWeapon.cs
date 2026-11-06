using Godot;

namespace SpaceExtraction.Ships;

/// <summary>
/// Distinct ship weapon roles for Milestone 2 (Section 12).
/// </summary>
public enum ShipWeaponType
{
    Autocannon = 0,
    Railgun = 1,
    EmpCannon = 2
}

/// <summary>
/// Ship weapon component supporting multiple weapon archetypes, cooldowns, magazine ammunition, and reload cycles.
/// </summary>
public sealed class ShipWeapon
{
    public ShipWeaponType WeaponType { get; init; } = ShipWeaponType.Autocannon;
    public string WeaponName { get; init; } = "Autocannon";
    public float FireCooldown { get; init; } = 0.22f;
    public float ProjectileSpeed { get; init; } = 950.0f;
    public float ProjectileDamage { get; init; } = 35.0f;
    public float SubsystemDamage { get; init; } = 15.0f;
    public float ProjectileLifetime { get; init; } = 1.8f;
    public int MaxAmmo { get; init; } = 20;
    public float ReloadDuration { get; init; } = 1.4f;
    public Color BoltColor { get; init; } = new Color(1.0f, 0.85f, 0.25f);
    public Vector2 LocalMuzzleOffset { get; init; } = new Vector2(198.0f, 0.0f);
    public float DamageMultiplier { get; set; } = 1.0f;

    public float EffectiveProjectileDamage => ProjectileDamage * DamageMultiplier;
    public float EffectiveSubsystemDamage => SubsystemDamage * DamageMultiplier;

    public int CurrentAmmo { get; private set; }
    public bool IsReloading { get; private set; }
    public float ReloadTimeRemaining { get; private set; }
    public float CooldownRemaining { get; private set; }

    public bool CanFire => !IsReloading && CooldownRemaining <= 0.0f && CurrentAmmo > 0;

    public ShipWeapon(int initialAmmo = 20)
    {
        MaxAmmo = Mathf.Max(1, initialAmmo);
        CurrentAmmo = MaxAmmo;
    }

    /// <summary>
    /// Autocannon: Reliable general hull damage with rapid fire and 20-round magazine.
    /// </summary>
    public static ShipWeapon CreateAutocannon()
    {
        return new ShipWeapon(20)
        {
            WeaponType = ShipWeaponType.Autocannon,
            WeaponName = "Autocannon",
            FireCooldown = 0.20f,
            ProjectileSpeed = 950.0f,
            ProjectileDamage = 35.0f,
            SubsystemDamage = 15.0f,
            ProjectileLifetime = 1.8f,
            ReloadDuration = 1.4f,
            BoltColor = new Color(1.0f, 0.85f, 0.25f),
            LocalMuzzleOffset = new Vector2(198.0f, 0.0f)
        };
    }

    /// <summary>
    /// Railgun: Long-range, high-velocity precision slug that inflicts heavy hull and subsystem pressure.
    /// </summary>
    public static ShipWeapon CreateRailgun()
    {
        return new ShipWeapon(5)
        {
            WeaponType = ShipWeaponType.Railgun,
            WeaponName = "Railgun",
            FireCooldown = 0.70f,
            ProjectileSpeed = 1650.0f,
            ProjectileDamage = 70.0f,
            SubsystemDamage = 65.0f,
            ProjectileLifetime = 1.75f,
            ReloadDuration = 2.0f,
            BoltColor = new Color(0.45f, 0.95f, 1.0f),
            LocalMuzzleOffset = new Vector2(198.0f, 0.0f)
        };
    }

    /// <summary>
    /// EMP Cannon: Specialized system-disabling pulse that strips subsystems (Engines/Weapons/Turrets)
    /// while dealing minimal structural hull damage.
    /// </summary>
    public static ShipWeapon CreateEmpCannon()
    {
        return new ShipWeapon(8)
        {
            WeaponType = ShipWeaponType.EmpCannon,
            WeaponName = "EMP Cannon",
            FireCooldown = 0.45f,
            ProjectileSpeed = 880.0f,
            ProjectileDamage = 8.0f,
            SubsystemDamage = 90.0f,
            ProjectileLifetime = 1.75f,
            ReloadDuration = 1.7f,
            BoltColor = new Color(0.65f, 0.35f, 1.0f),
            LocalMuzzleOffset = new Vector2(198.0f, 0.0f)
        };
    }

    /// <summary>
    /// Dorsal Turret Autocannon: Independent 360-degree turret weapon.
    /// </summary>
    public static ShipWeapon CreateTurretCannon()
    {
        return new ShipWeapon(30)
        {
            WeaponType = ShipWeaponType.Autocannon,
            WeaponName = "Dorsal Turret",
            FireCooldown = 0.24f,
            ProjectileSpeed = 1050.0f,
            ProjectileDamage = 24.0f,
            SubsystemDamage = 12.0f,
            ProjectileLifetime = 1.75f,
            ReloadDuration = 1.5f,
            BoltColor = new Color(1.0f, 0.55f, 0.2f),
            LocalMuzzleOffset = new Vector2(28.0f, 0.0f)
        };
    }

    public void StepCooldown(float delta)
    {
        if (delta <= 0.0f)
        {
            return;
        }

        if (CooldownRemaining > 0.0f)
        {
            CooldownRemaining = Mathf.Max(0.0f, CooldownRemaining - delta);
        }

        if (IsReloading)
        {
            ReloadTimeRemaining = Mathf.Max(0.0f, ReloadTimeRemaining - delta);
            if (ReloadTimeRemaining <= 0.0f)
            {
                IsReloading = false;
                CurrentAmmo = MaxAmmo;
            }
        }
    }

    public bool StartReload()
    {
        if (IsReloading || CurrentAmmo >= MaxAmmo)
        {
            return false;
        }

        IsReloading = true;
        ReloadTimeRemaining = ReloadDuration;
        return true;
    }

    public bool TryFire(
        bool triggerRequested,
        bool isWeaponDisabled,
        Vector2 originWorldPosition,
        float aimRotationRadians,
        out Vector2 muzzleWorldPosition,
        out Vector2 fireDirection,
        float cooldownMultiplier = 1.0f)
    {
        Vector2 forward = Vector2.Right.Rotated(aimRotationRadians);
        muzzleWorldPosition = originWorldPosition + LocalMuzzleOffset.Rotated(aimRotationRadians);
        fireDirection = forward;

        if (!triggerRequested || isWeaponDisabled || IsReloading || CooldownRemaining > 0.0f)
        {
            return false;
        }

        if (CurrentAmmo <= 0)
        {
            StartReload();
            return false;
        }

        CurrentAmmo--;
        CooldownRemaining = FireCooldown * Mathf.Max(0.1f, cooldownMultiplier);

        if (CurrentAmmo <= 0)
        {
            StartReload();
        }

        return true;
    }

    public void SetStateFromReplication(int currentAmmo, bool isReloading, float reloadRemaining)
    {
        CurrentAmmo = Mathf.Clamp(currentAmmo, 0, MaxAmmo);
        IsReloading = isReloading;
        ReloadTimeRemaining = Mathf.Max(0.0f, reloadRemaining);
    }
}
