using Godot;

namespace SpaceExtraction.Interiors;

/// <summary>
/// Operational console station archetypes inside a ship (Section 31 & Milestone 3).
/// </summary>
public enum InteriorConsoleType
{
    None = 0,
    Pilot = 1,
    Turret = 2,
    Engineering = 3,
    Cargo = 4,
    Medical = 5
}

/// <summary>
/// An interactable physical station console located inside an interior room compartment.
/// Players can approach and interact to assume station roles or trigger subsystem functions.
/// </summary>
public sealed class InteriorConsole
{
    public int ConsoleId { get; init; }
    public InteriorConsoleType ConsoleType { get; init; }
    public string ConsoleName { get; init; } = string.Empty;
    public InteriorRoomType RoomType { get; init; }
    public Vector2 LocalPosition { get; init; }
    public Vector2 SeatOrStandLocalPosition { get; init; }
    public Rect2 VisualRect { get; init; }
    public Color ConsoleColor { get; init; }
    public float InteractionRadius { get; init; } = 24.0f;
}
