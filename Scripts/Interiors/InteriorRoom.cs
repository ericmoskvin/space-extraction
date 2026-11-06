using Godot;

namespace SpaceExtraction.Interiors;

/// <summary>
/// Architectural room classifications within a ship's physical interior (Section 31 & Milestone 3).
/// </summary>
public enum InteriorRoomType
{
    None = 0,
    Cockpit = 1,
    CentralHub = 2,
    EngineRoom = 3,
    Cargo = 4,
    Medical = 5
}

/// <summary>
/// Represents a discrete physical room compartment within a ship interior,
/// defining structural bounds, character walkable bounds, and presentation styling.
/// </summary>
public sealed class InteriorRoom
{
    public InteriorRoomType RoomType { get; init; }
    public string RoomName { get; init; } = string.Empty;
    public Rect2 Bounds { get; init; }
    public Rect2 WalkableRect { get; init; }
    public Color FloorColor { get; init; }

    public bool ContainsPoint(Vector2 localPoint) => Bounds.HasPoint(localPoint);
}
