namespace SpaceExtraction.Ships;

/// <summary>
/// Core ship archetypes defined in Sections 8 and 9 of GAME_DESIGN.md.
/// </summary>
public enum ShipClassType
{
    Scout = 0,
    Corvette = 1,
    Freighter = 2
}

/// <summary>
/// Defines movement, boost, and baseline hull parameters for a ship class.
/// </summary>
public sealed class ShipMovementProfile
{
    public ShipClassType ShipClass { get; init; }
    public float ForwardAcceleration { get; init; }
    public float ReverseAcceleration { get; init; }
    public float MaxSpeed { get; init; }
    public float TurnSpeedRadians { get; init; }
    public float BrakingDeceleration { get; init; }
    public float PassiveDrag { get; init; }
    public float BoostSpeedMultiplier { get; init; }
    public float BoostAccelerationMultiplier { get; init; }
    public float BoostDuration { get; init; }
    public float BoostCooldown { get; init; }
    public float MaxHullIntegrity { get; init; }
    public float CollisionRadius { get; init; }

    public static ShipMovementProfile CreateScout()
    {
        return new ShipMovementProfile
        {
            ShipClass = ShipClassType.Scout,
            ForwardAcceleration = 520.0f,
            ReverseAcceleration = 320.0f,
            MaxSpeed = 480.0f,
            TurnSpeedRadians = 3.8f,
            BrakingDeceleration = 620.0f,
            PassiveDrag = 45.0f,
            BoostSpeedMultiplier = 1.65f,
            BoostAccelerationMultiplier = 2.2f,
            BoostDuration = 1.4f,
            BoostCooldown = 3.5f,
            MaxHullIntegrity = 160.0f,
            CollisionRadius = 22.0f
        };
    }

    /// <summary>
    /// Primary reference ship profile for MVP development (Section 9).
    /// </summary>
    public static ShipMovementProfile CreateCorvette()
    {
        return new ShipMovementProfile
        {
            ShipClass = ShipClassType.Corvette,
            ForwardAcceleration = 380.0f,
            ReverseAcceleration = 240.0f,
            MaxSpeed = 360.0f,
            TurnSpeedRadians = 2.8f,
            BrakingDeceleration = 480.0f,
            PassiveDrag = 35.0f,
            BoostSpeedMultiplier = 1.55f,
            BoostAccelerationMultiplier = 2.0f,
            BoostDuration = 1.5f,
            BoostCooldown = 4.0f,
            MaxHullIntegrity = 240.0f,
            CollisionRadius = 28.0f
        };
    }

    public static ShipMovementProfile CreateFreighter()
    {
        return new ShipMovementProfile
        {
            ShipClass = ShipClassType.Freighter,
            ForwardAcceleration = 220.0f,
            ReverseAcceleration = 140.0f,
            MaxSpeed = 240.0f,
            TurnSpeedRadians = 1.6f,
            BrakingDeceleration = 300.0f,
            PassiveDrag = 25.0f,
            BoostSpeedMultiplier = 1.4f,
            BoostAccelerationMultiplier = 1.75f,
            BoostDuration = 1.8f,
            BoostCooldown = 5.5f,
            MaxHullIntegrity = 480.0f,
            CollisionRadius = 38.0f
        };
    }

    public static ShipMovementProfile FromClass(ShipClassType shipClass)
    {
        return shipClass switch
        {
            ShipClassType.Scout => CreateScout(),
            ShipClassType.Freighter => CreateFreighter(),
            _ => CreateCorvette()
        };
    }
}
