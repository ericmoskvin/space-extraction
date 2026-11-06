using Godot;

namespace SpaceExtraction.Ships;

/// <summary>
/// Server-authoritative ship flight dynamics (Section 8, Section 11 & Milestones 1–2).
/// Computes rotation, forward/reverse thrust, active braking, temporary boost,
/// and engine subsystem damage penalties (reduced acceleration/top speed or immobilization).
/// </summary>
public sealed class ShipMovement
{
    public ShipMovementProfile Profile { get; }
    public bool IsBoosting { get; private set; }
    public float BoostTimeRemaining { get; private set; }
    public float BoostCooldownRemaining { get; private set; }

    public ShipMovement(ShipMovementProfile profile)
    {
        Profile = profile;
    }

    /// <summary>
    /// Steps the ship's rotation and linear velocity for a single physics frame.
    /// Engine efficiency in [0, 1] scales thrust acceleration and top speed; 0 immobilizes thrust/boost.
    /// </summary>
    public void StepPhysics(
        ShipControlInput rawInput,
        bool isDisabled,
        float delta,
        ref float rotationRadians,
        ref Vector2 velocity,
        float engineEfficiency = 1.0f)
    {
        if (delta <= 0.0f)
        {
            return;
        }

        ShipControlInput input = rawInput.Sanitized();
        float clampedEngineEfficiency = Mathf.Clamp(engineEfficiency, 0.0f, 1.0f);

        // Update boost timers
        if (BoostTimeRemaining > 0.0f)
        {
            BoostTimeRemaining = Mathf.Max(0.0f, BoostTimeRemaining - delta);
            if (BoostTimeRemaining <= 0.0f)
            {
                IsBoosting = false;
            }
        }

        if (BoostCooldownRemaining > 0.0f)
        {
            BoostCooldownRemaining = Mathf.Max(0.0f, BoostCooldownRemaining - delta);
        }

        if (isDisabled || clampedEngineEfficiency <= 0.0f)
        {
            IsBoosting = false;
            BoostTimeRemaining = 0.0f;
            velocity = velocity.MoveToward(Vector2.Zero, Profile.PassiveDrag * 1.5f * delta);
            return;
        }

        // Trigger boost if requested and off cooldown
        if (input.Boost && !IsBoosting && BoostCooldownRemaining <= 0.0f)
        {
            IsBoosting = true;
            BoostTimeRemaining = Profile.BoostDuration;
            BoostCooldownRemaining = Profile.BoostCooldown;
        }

        // Rotation / turning
        if (Mathf.Abs(input.Turn) > 0.001f)
        {
            float turnEfficiency = Mathf.Lerp(0.5f, 1.0f, clampedEngineEfficiency);
            rotationRadians = Mathf.Wrap(
                rotationRadians + input.Turn * Profile.TurnSpeedRadians * turnEfficiency * delta,
                -Mathf.Pi,
                Mathf.Pi);
        }

        Vector2 forward = Vector2.Right.Rotated(rotationRadians);
        float effectiveMaxSpeed = Profile.MaxSpeed * clampedEngineEfficiency * (IsBoosting ? Profile.BoostSpeedMultiplier : 1.0f);

        // Active braking
        if (input.Brake && !IsBoosting)
        {
            velocity = velocity.MoveToward(Vector2.Zero, Profile.BrakingDeceleration * clampedEngineEfficiency * delta);
            return;
        }

        // Thrust acceleration (if boosting without explicit throttle, apply forward boost thrust)
        float effectiveThrottle = input.Throttle;
        if (IsBoosting && effectiveThrottle <= 0.0f)
        {
            effectiveThrottle = 1.0f;
        }

        if (Mathf.Abs(effectiveThrottle) > 0.001f)
        {
            float accel = (effectiveThrottle > 0.0f
                ? Profile.ForwardAcceleration * (IsBoosting ? Profile.BoostAccelerationMultiplier : 1.0f)
                : Profile.ReverseAcceleration) * clampedEngineEfficiency;

            velocity += forward * (effectiveThrottle * accel * delta);

            float speed = velocity.Length();
            if (speed > effectiveMaxSpeed)
            {
                velocity = velocity.Normalized() * Mathf.MoveToward(speed, effectiveMaxSpeed, Profile.BrakingDeceleration * delta);
            }
        }
        else
        {
            // Light passive space damping when coasting
            velocity = velocity.MoveToward(Vector2.Zero, Profile.PassiveDrag * delta);
        }
    }

    public void SetStateFromReplication(bool isBoosting, float boostCooldownRemaining)
    {
        IsBoosting = isBoosting;
        BoostCooldownRemaining = Mathf.Max(0.0f, boostCooldownRemaining);
    }
}
