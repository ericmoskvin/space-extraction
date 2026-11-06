using System;
using Godot;

namespace SpaceExtraction.Ships;

/// <summary>
/// Tracks structural hull integrity and disabled status for a ship (Section 11 & Milestone 1).
/// When hull reaches 0, the ship becomes disabled rather than immediately disappearing,
/// laying the foundation for boarding and capture in later milestones.
/// </summary>
public sealed class ShipHull
{
    public float MaxHull { get; private set; }
    public float CurrentHull { get; private set; }
    public bool IsDisabled { get; private set; }
    public int LastAttackerShipId { get; private set; }

    public float HullFraction => MaxHull > 0.0f ? Mathf.Clamp(CurrentHull / MaxHull, 0.0f, 1.0f) : 0.0f;

    public event Action<float, float, bool>? HullChanged;
    public event Action<int>? ShipDisabled;

    public ShipHull(float maxHull)
    {
        MaxHull = Mathf.Max(1.0f, maxHull);
        CurrentHull = MaxHull;
        IsDisabled = false;
    }

    public void ApplyDamage(float amount, int attackerShipId = 0)
    {
        if (amount <= 0.0f || !float.IsFinite(amount))
        {
            return;
        }

        bool wasDisabled = IsDisabled;
        LastAttackerShipId = attackerShipId;
        CurrentHull = Mathf.Max(0.0f, CurrentHull - amount);

        if (CurrentHull <= 0.0f)
        {
            IsDisabled = true;
        }

        HullChanged?.Invoke(CurrentHull, MaxHull, IsDisabled);

        if (!wasDisabled && IsDisabled)
        {
            ShipDisabled?.Invoke(attackerShipId);
        }
    }

    public void Repair(float amount)
    {
        if (amount <= 0.0f || !float.IsFinite(amount))
        {
            return;
        }

        CurrentHull = Mathf.Min(MaxHull, CurrentHull + amount);
        if (CurrentHull > 0.0f)
        {
            IsDisabled = false;
        }

        HullChanged?.Invoke(CurrentHull, MaxHull, IsDisabled);
    }

    public void SetStateFromReplication(float currentHull, float maxHull, bool isDisabled)
    {
        bool wasDisabled = IsDisabled;
        MaxHull = Mathf.Max(1.0f, maxHull);
        CurrentHull = Mathf.Clamp(currentHull, 0.0f, MaxHull);
        IsDisabled = isDisabled;

        HullChanged?.Invoke(CurrentHull, MaxHull, IsDisabled);
        if (!wasDisabled && IsDisabled)
        {
            ShipDisabled?.Invoke(LastAttackerShipId);
        }
    }
}
