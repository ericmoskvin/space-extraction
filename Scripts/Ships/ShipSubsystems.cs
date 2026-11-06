using System;
using Godot;

namespace SpaceExtraction.Ships;

/// <summary>
/// Discrete ship subsystems that can be damaged, disabled, and repaired (Section 11 & Milestone 2).
/// </summary>
public enum ShipSubsystemType
{
    None = 0,
    Engines = 1,
    Weapons = 2,
    Turret = 3
}

/// <summary>
/// Tracks health and operational efficiency of a ship's discrete subsystems:
/// Engines (affecting speed/acceleration/immobilization), Primary Weapons, and Turret.
/// </summary>
public sealed class ShipSubsystems
{
    public float MaxEngineHealth { get; private set; }
    public float CurrentEngineHealth { get; private set; }

    public float MaxWeaponsHealth { get; private set; }
    public float CurrentWeaponsHealth { get; private set; }

    public float MaxTurretHealth { get; private set; }
    public float CurrentTurretHealth { get; private set; }

    public bool IsEnginesDisabled => CurrentEngineHealth <= 0.0f;
    public bool IsWeaponsDisabled => CurrentWeaponsHealth <= 0.0f;
    public bool IsTurretDisabled => CurrentTurretHealth <= 0.0f;

    /// <summary>
    /// Returns movement multiplier in [0, 1].
    /// 0 when engines are disabled (complete immobilization); scales from 0.35 to 1.0 as engine health varies.
    /// </summary>
    public float EngineEfficiency
    {
        get
        {
            if (CurrentEngineHealth <= 0.0f || MaxEngineHealth <= 0.0f)
            {
                return 0.0f;
            }

            float ratio = Mathf.Clamp(CurrentEngineHealth / MaxEngineHealth, 0.0f, 1.0f);
            if (ratio >= 0.99f)
            {
                return 1.0f;
            }

            return Mathf.Lerp(0.35f, 1.0f, ratio);
        }
    }

    public event Action? SubsystemsChanged;

    public ShipSubsystems(float maxEngineHealth = 120.0f, float maxWeaponsHealth = 100.0f, float maxTurretHealth = 100.0f)
    {
        MaxEngineHealth = Mathf.Max(1.0f, maxEngineHealth);
        CurrentEngineHealth = MaxEngineHealth;

        MaxWeaponsHealth = Mathf.Max(1.0f, maxWeaponsHealth);
        CurrentWeaponsHealth = MaxWeaponsHealth;

        MaxTurretHealth = Mathf.Max(1.0f, maxTurretHealth);
        CurrentTurretHealth = MaxTurretHealth;
    }

    public void ApplySubsystemDamage(ShipSubsystemType subsystem, float amount)
    {
        if (amount <= 0.0f || !float.IsFinite(amount) || subsystem == ShipSubsystemType.None)
        {
            return;
        }

        switch (subsystem)
        {
            case ShipSubsystemType.Engines:
                CurrentEngineHealth = Mathf.Max(0.0f, CurrentEngineHealth - amount);
                break;
            case ShipSubsystemType.Weapons:
                CurrentWeaponsHealth = Mathf.Max(0.0f, CurrentWeaponsHealth - amount);
                break;
            case ShipSubsystemType.Turret:
                CurrentTurretHealth = Mathf.Max(0.0f, CurrentTurretHealth - amount);
                break;
        }

        SubsystemsChanged?.Invoke();
    }

    public void RepairSubsystem(ShipSubsystemType subsystem, float amount)
    {
        if (amount <= 0.0f || !float.IsFinite(amount))
        {
            return;
        }

        switch (subsystem)
        {
            case ShipSubsystemType.Engines:
                CurrentEngineHealth = Mathf.Min(MaxEngineHealth, CurrentEngineHealth + amount);
                break;
            case ShipSubsystemType.Weapons:
                CurrentWeaponsHealth = Mathf.Min(MaxWeaponsHealth, CurrentWeaponsHealth + amount);
                break;
            case ShipSubsystemType.Turret:
                CurrentTurretHealth = Mathf.Min(MaxTurretHealth, CurrentTurretHealth + amount);
                break;
        }

        SubsystemsChanged?.Invoke();
    }

    public void SetStateFromReplication(float engineHealth, float weaponsHealth, float turretHealth)
    {
        CurrentEngineHealth = Mathf.Clamp(engineHealth, 0.0f, MaxEngineHealth);
        CurrentWeaponsHealth = Mathf.Clamp(weaponsHealth, 0.0f, MaxWeaponsHealth);
        CurrentTurretHealth = Mathf.Clamp(turretHealth, 0.0f, MaxTurretHealth);
        SubsystemsChanged?.Invoke();
    }
}
