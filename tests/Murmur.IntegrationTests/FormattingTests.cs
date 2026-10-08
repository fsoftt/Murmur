using Murmur.Domain.Model;
using Murmur.Presentation.Formatting;

namespace Murmur.IntegrationTests;

public class FormattingTests
{
    [Theory]
    [InlineData("Ana Ruiz", "AR")]
    [InlineData("  beto  ", "B")]
    [InlineData("María José Pérez", "MJ")]
    [InlineData("", "?")]
    [InlineData(null, "?")]
    [InlineData("¡Hola!", "H")]
    public void Initials_take_the_first_letters_of_up_to_two_words(string? name, string expected) =>
        Assert.Equal(expected, Avatars.Initials(name));

    [Fact]
    public void Avatar_color_is_stable_for_an_identity()
    {
        var key = new PublicKey(Enumerable.Range(0, 32).Select(i => (byte)(i * 7)).ToArray());

        Assert.Equal(Avatars.ColorFor(key), Avatars.ColorFor(new PublicKey(key.ToArray())));
        Assert.Matches("^#[0-9A-F]{6}$", Avatars.ColorFor(key));
    }

    [Fact]
    public void Fingerprint_is_mirrored_and_depends_on_the_safety_number()
    {
        var cells = Fingerprint.Cells("12345 67890 11111");

        Assert.Equal(64, cells.Count);
        for (var row = 0; row < 8; row++)
        {
            for (var col = 0; col < 8; col++)
            {
                Assert.Equal(cells[row * 8 + col], cells[row * 8 + 7 - col]);
            }
        }

        Assert.All(cells, c => Assert.InRange(c, -1, 3));
        Assert.NotEqual(cells, Fingerprint.Cells("12345 67890 11112"));
    }

    [Fact]
    public void Activity_time_is_short_and_relative()
    {
        var now = new DateTimeOffset(2026, 10, 8, 18, 0, 0, TimeSpan.Zero).ToLocalTime();

        Assert.Equal(now.AddMinutes(-5).ToString("HH:mm", System.Globalization.CultureInfo.CurrentCulture), UserMessages.ForActivity(now.AddMinutes(-5), now));
        Assert.Equal("Ayer", UserMessages.ForActivity(now.AddDays(-1), now));
        Assert.Equal(string.Empty, UserMessages.ForActivity(null, now));
    }
}
