using Godot;

namespace SpaceExtraction.Characters;

/// <summary>
/// Represents the network identity and metadata of a connected player.
/// Decoupled from any specific ship or physical body instance.
/// </summary>
public sealed class PlayerIdentity
{
    public const int MaxDisplayNameLength = 24;

    public long PeerId { get; }
    public string DisplayName { get; }
    public int TeamId { get; }
    public Color PlayerColor { get; }

    public PlayerIdentity(long peerId, string displayName, int teamId, Color playerColor)
    {
        PeerId = peerId;
        DisplayName = SanitizeDisplayName(displayName, peerId);
        TeamId = teamId;
        PlayerColor = playerColor;
    }

    public static string SanitizeDisplayName(string? rawName, long peerId)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return $"Player-{peerId}";
        }

        string cleaned = rawName.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
        if (cleaned.Length == 0)
        {
            return $"Player-{peerId}";
        }

        return cleaned.Length > MaxDisplayNameLength ? cleaned[..MaxDisplayNameLength] : cleaned;
    }

    public static Color GetDeterministicColor(long peerId)
    {
        if (peerId == 1)
        {
            return new Color(0.25f, 0.85f, 1.0f); // Cyan for host/server player
        }

        float hue = Mathf.PosMod(peerId * 0.61803398875f, 1.0f);
        return Color.FromHsv(hue, 0.75f, 0.95f);
    }
}
