using System.Security.Cryptography;
using System.Text;

namespace Directo.Presentation.Formatting;

/// <summary>
/// An 8×8, left-right mirrored pattern derived from the safety number. Both devices compute the
/// same safety number, so both draw the same picture, which is quicker to compare than 60 digits.
/// Cells hold a palette index from 0 to 3, or -1 for empty.
/// </summary>
public static class Fingerprint
{
    public const int Size = 8;

    public static IReadOnlyList<int> Cells(string safetyNumber)
    {
        ArgumentNullException.ThrowIfNull(safetyNumber);
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(safetyNumber.Replace(" ", string.Empty, StringComparison.Ordinal)));
        var cells = new int[Size * Size];
        for (var row = 0; row < Size; row++)
        {
            for (var col = 0; col < Size / 2; col++)
            {
                var value = hash[row * (Size / 2) + col] % 5;
                var cell = value == 4 ? -1 : value;
                cells[row * Size + col] = cell;
                cells[row * Size + (Size - 1 - col)] = cell;
            }
        }

        return cells;
    }

    /// <summary>The safety number as twelve 5-digit groups.</summary>
    public static IReadOnlyList<string> Groups(string safetyNumber) =>
        (safetyNumber ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
