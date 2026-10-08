namespace Murmur.App.Graphics;

/// <summary>Reads the Light/Dark design tokens from Colors.xaml for code that draws directly.</summary>
public static class Theme
{
    public static bool IsDark => Application.Current?.RequestedTheme == AppTheme.Dark;

    /// <summary>A color resource by its exact key.</summary>
    public static Color Resource(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color ? color : Colors.Gray;

    public static Color Get(string role)
    {
        var resources = Application.Current?.Resources;
        if (resources is null)
        {
            return Colors.Gray;
        }

        if (resources.TryGetValue(role + (IsDark ? "Dark" : "Light"), out var themed) && themed is Color color)
        {
            return color;
        }

        return resources.TryGetValue(role, out var plain) && plain is Color fixedColor ? fixedColor : Colors.Gray;
    }
}
