using Godot;

namespace SpaceExtraction.Ships;

/// <summary>
/// Basic ship weapon component (Autocannon) for Milestone 1.
/// Manages fire cooldown and calculates projectile spawn parameters.
/// </summary>
public sealed class ShipWeapon
{
    public string WeaponName { get; init; } = "Autocannon";
    public float FireCooldown { get; init; } = 0.22f;
    public float ProjectileSpeed { get; init; } = 950.0f;
    public float ProjectileDamage { get; init; } = 40.0f;
    public float ProjectileLifetime { get; init; } = 1.8f;
    public Vector2 LocalMuzzleOffset { get; init; } = new Vector2(34.0f, 0.0f);

    public float CooldownRemaining { get; private set; }
    public bool CanFire => CooldownRemaining <= 0.0f;

    public void StepCooldown(float delta)
    {
        if (CooldownRemaining > 0.0f && delta > 0.0f)
        {
            CooldownRemaining = Mathf.Max(0.0f, CooldownRemaining - delta);
        }
    }

    public bool TryFire(
        bool triggerRequested,
        bool isShipDisabled,
        Vector2 shipGlobalPosition,
        float shipRotationRadians,
        out Vector2 muzzleWorldPosition,
        out Vector2 fireDirection)
    {
        Vector2 forward = Vector2.Right.Rotated(shipRotationRadians);
        muzzleWorldPosition = shipGlobalPosition + LocalMuzzleOffset.Rotated(shipRotationRadians);
        fireDirection = forward;

        if (!triggerRequested || isShipDisabled || CooldownRemaining > 0.0f)
        {
            return false;
        }

        CooldownRemaining = FireCooldown;
        return true;
    }
}
