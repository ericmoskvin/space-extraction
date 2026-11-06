using Godot;

namespace SpaceExtraction.Characters;

/// <summary>
/// Represents a single frame/tick of client movement input sent to the authoritative server.
/// </summary>
public struct PlayerInputState
{
    public Vector2 MoveDirection { get; set; }
    public uint Tick { get; set; }

    /// <summary>
    /// Validates and clamps untrusted client movement input on the server.
    /// Rejects NaN/Inf values and clamps vector magnitude to at most 1.0.
    /// </summary>
    public static Vector2 SanitizeDirection(Vector2 rawDirection)
    {
        if (!float.IsFinite(rawDirection.X) || !float.IsFinite(rawDirection.Y))
        {
            return Vector2.Zero;
        }

        float lengthSq = rawDirection.LengthSquared();
        if (lengthSq > 1.0f)
        {
            return rawDirection / Mathf.Sqrt(lengthSq);
        }

        if (lengthSq < 0.0001f)
        {
            return Vector2.Zero;
        }

        return rawDirection;
    }
}
