using Godot;

namespace SpaceExtraction.Interiors;

/// <summary>
/// Connective bulkhead door bridging two adjacent interior room compartments (Section 31 & Milestone 3).
/// Can be toggled open or closed to allow or restrict physical pedestrian traversal.
/// </summary>
public sealed class InteriorDoor
{
    public int DoorId { get; init; }
    public string DoorName { get; init; } = string.Empty;
    public InteriorRoomType RoomA { get; init; }
    public InteriorRoomType RoomB { get; init; }
    public Vector2 LocalPosition { get; init; }
    public bool IsVerticalBulkhead { get; init; }
    public Rect2 VisualRect { get; init; }
    public Rect2 WalkableConnectorRect { get; init; }
    public float InteractionRadius { get; init; } = 24.0f;
    public bool IsOpen { get; set; } = true;
}
