using Godot;

namespace SpaceExtraction.Ships;

/// <summary>
/// Independent ship turret component (Section 11 & Milestone 2).
/// Supports Automated AI defense mode when unmanned, and Manual operator control mode
/// with superior turn rate, range, and fire rate when manned by a player.
/// </summary>
public sealed class ShipTurret
{
    public const float AutoAcquireRange = 460.0f;
    public const float ManualEffectiveRange = 850.0f;
    public const float AutoTurnSpeedRadians = 2.4f;
    public const float ManualTurnSpeedRadians = 7.5f;
    public const float AutoFireAlignmentTolerance = 0.20f;
    public const float AutoCooldownMultiplier = 1.65f;
    public const float ManualCooldownMultiplier = 0.75f;

    public Vector2 LocalMountOffset { get; init; } = new Vector2(-4.0f, 0.0f);
    public ShipWeapon Weapon { get; } = ShipWeapon.CreateTurretCannon();

    public long GunnerPeerId { get; private set; }
    public bool IsAutomatedMode => GunnerPeerId <= 0;
    public bool IsManuallyOperated => GunnerPeerId > 0;
    public float WorldAimRotation { get; private set; }
    public int CurrentAutoTargetShipId { get; private set; }

    public void SetGunner(long peerId)
    {
        GunnerPeerId = peerId > 0 ? peerId : 0;
        if (GunnerPeerId > 0)
        {
            CurrentAutoTargetShipId = 0;
        }
    }

    public Vector2 GetMountWorldPosition(Vector2 shipGlobalPosition, float shipRotationRadians)
    {
        return shipGlobalPosition + LocalMountOffset.Rotated(shipRotationRadians);
    }

    /// <summary>
    /// Steps the turret in Manual Control mode using the operator's desired world aim angle and fire trigger.
    /// </summary>
    public bool StepManualServer(
        float delta,
        bool isDisabled,
        Vector2 shipGlobalPosition,
        float shipRotationRadians,
        float desiredWorldAimAngle,
        bool fireTriggerHeld,
        out Vector2 muzzleWorldPos,
        out Vector2 fireDir)
    {
        Weapon.StepCooldown(delta);
        Vector2 mountPos = GetMountWorldPosition(shipGlobalPosition, shipRotationRadians);
        muzzleWorldPos = mountPos;
        fireDir = Vector2.Right.Rotated(WorldAimRotation);

        if (isDisabled)
        {
            return false;
        }

        float angleDiff = Mathf.AngleDifference(WorldAimRotation, desiredWorldAimAngle);
        float maxStep = ManualTurnSpeedRadians * delta;
        WorldAimRotation = Mathf.Wrap(
            WorldAimRotation + Mathf.Clamp(angleDiff, -maxStep, maxStep),
            -Mathf.Pi,
            Mathf.Pi);

        return Weapon.TryFire(
            fireTriggerHeld,
            false,
            mountPos,
            WorldAimRotation,
            out muzzleWorldPos,
            out fireDir,
            ManualCooldownMultiplier);
    }

    /// <summary>
    /// Steps the turret in Automated AI Defense mode, tracking the nearest valid enemy ship within range.
    /// </summary>
    public bool StepAutomatedServer(
        float delta,
        bool isDisabled,
        Vector2 shipGlobalPosition,
        float shipRotationRadians,
        Ship? nearestEnemyTarget,
        out Vector2 muzzleWorldPos,
        out Vector2 fireDir)
    {
        Weapon.StepCooldown(delta);
        Vector2 mountPos = GetMountWorldPosition(shipGlobalPosition, shipRotationRadians);
        muzzleWorldPos = mountPos;
        fireDir = Vector2.Right.Rotated(WorldAimRotation);

        if (isDisabled)
        {
            CurrentAutoTargetShipId = 0;
            return false;
        }

        if (nearestEnemyTarget == null || nearestEnemyTarget.Hull.IsDisabled)
        {
            CurrentAutoTargetShipId = 0;
            // Return smoothly toward forward ship heading when idle
            float idleDiff = Mathf.AngleDifference(WorldAimRotation, shipRotationRadians);
            float idleStep = AutoTurnSpeedRadians * 0.5f * delta;
            WorldAimRotation = Mathf.Wrap(
                WorldAimRotation + Mathf.Clamp(idleDiff, -idleStep, idleStep),
                -Mathf.Pi,
                Mathf.Pi);
            return false;
        }

        CurrentAutoTargetShipId = nearestEnemyTarget.ShipId;
        Vector2 toTarget = nearestEnemyTarget.GlobalPosition - mountPos;
        float dist = toTarget.Length();
        if (dist > AutoAcquireRange)
        {
            CurrentAutoTargetShipId = 0;
            return false;
        }

        // First-order target lead estimation
        float timeToHit = dist / Mathf.Max(100.0f, Weapon.ProjectileSpeed);
        Vector2 predictedPos = nearestEnemyTarget.GlobalPosition + nearestEnemyTarget.Velocity * (timeToHit * 0.65f);
        float targetAngle = (predictedPos - mountPos).Angle();

        float diff = Mathf.AngleDifference(WorldAimRotation, targetAngle);
        float maxTurn = AutoTurnSpeedRadians * delta;
        WorldAimRotation = Mathf.Wrap(
            WorldAimRotation + Mathf.Clamp(diff, -maxTurn, maxTurn),
            -Mathf.Pi,
            Mathf.Pi);

        bool aligned = Mathf.Abs(Mathf.AngleDifference(WorldAimRotation, targetAngle)) <= AutoFireAlignmentTolerance;
        return Weapon.TryFire(
            aligned,
            false,
            mountPos,
            WorldAimRotation,
            out muzzleWorldPos,
            out fireDir,
            AutoCooldownMultiplier);
    }

    public void SetStateFromReplication(long gunnerPeerId, float worldAimRotation, int autoTargetShipId)
    {
        GunnerPeerId = gunnerPeerId;
        WorldAimRotation = worldAimRotation;
        CurrentAutoTargetShipId = autoTargetShipId;
    }
}
