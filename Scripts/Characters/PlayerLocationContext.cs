using Godot;

namespace SpaceExtraction.Characters;

/// <summary>
/// Defines where a player currently resides within the shared simulation.
/// Follows Section 31 of GAME_DESIGN.md: Players are not tightly coupled to ships;
/// they track their current location/occupancy so they can transition between ships and space.
/// </summary>
public enum PlayerLocationMode
{
    WorldSpace = 0,
    ShipInterior = 1
}

/// <summary>
/// Tracks a player's current occupancy context (e.g., open space vs. aboard/piloting a specific ship).
/// </summary>
public struct PlayerLocationContext
{
    public PlayerLocationMode Mode { get; set; }
    public int OccupiedShipId { get; set; }
    public bool IsPiloting { get; set; }
    public bool IsOperatingTurret { get; set; }
    public NodePath OccupiedShipPath { get; set; }

    public bool IsOnFootInShip => Mode == PlayerLocationMode.ShipInterior && !IsPiloting && !IsOperatingTurret;

    public static PlayerLocationContext CreateWorldSpace()
    {
        return new PlayerLocationContext
        {
            Mode = PlayerLocationMode.WorldSpace,
            OccupiedShipId = 0,
            IsPiloting = false,
            IsOperatingTurret = false,
            OccupiedShipPath = new NodePath()
        };
    }

    public static PlayerLocationContext CreateShipOccupancy(int shipId, bool isPiloting, NodePath shipPath, bool isOperatingTurret = false)
    {
        return new PlayerLocationContext
        {
            Mode = PlayerLocationMode.ShipInterior,
            OccupiedShipId = shipId,
            IsPiloting = isPiloting,
            IsOperatingTurret = isOperatingTurret,
            OccupiedShipPath = shipPath
        };
    }
}
