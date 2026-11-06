using Godot;

namespace SpaceExtraction.Ships;

/// <summary>
/// Represents a single tick of pilot flight and weapon control inputs sent to the authoritative server.
/// </summary>
public struct ShipControlInput
{
    public float Throttle { get; set; }
    public float Turn { get; set; }
    public bool Brake { get; set; }
    public bool Boost { get; set; }
    public bool FirePrimary { get; set; }
    public uint Tick { get; set; }

    public ShipControlInput Sanitized()
    {
        float throttle = float.IsFinite(Throttle) ? Mathf.Clamp(Throttle, -1.0f, 1.0f) : 0.0f;
        float turn = float.IsFinite(Turn) ? Mathf.Clamp(Turn, -1.0f, 1.0f) : 0.0f;

        if (Mathf.Abs(throttle) < 0.001f)
        {
            throttle = 0.0f;
        }

        if (Mathf.Abs(turn) < 0.001f)
        {
            turn = 0.0f;
        }

        return new ShipControlInput
        {
            Throttle = throttle,
            Turn = turn,
            Brake = Brake,
            Boost = Boost,
            FirePrimary = FirePrimary,
            Tick = Tick
        };
    }
}
