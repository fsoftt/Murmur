using Murmur.Domain.Model;

namespace Murmur.Presentation.Formatting;

/// <summary>
/// Contact avatars without photos: initials on a color derived from the identity key, so the
/// same person always gets the same color on this device. Every color keeps white text at
/// 4.5:1 contrast or better.
/// </summary>
public static class Avatars
{
    private static readonly string[] Palette =
    [
        "#7C3AED", "#DB2777", "#0F766E", "#C2410C", "#2563EB", "#047857", "#9333EA", "#B45309",
    ];

    public static string Initials(string? name)
    {
        var words = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var letters = words
            .Select(w => w.FirstOrDefault(char.IsLetterOrDigit))
            .Where(c => c != default)
            .Take(2)
            .Select(char.ToUpperInvariant)
            .ToArray();
        return letters.Length == 0 ? "?" : new string(letters);
    }

    public static string ColorFor(PublicKey identityKey)
    {
        ArgumentNullException.ThrowIfNull(identityKey);
        return Palette[identityKey.Span[0] % Palette.Length];
    }
}
