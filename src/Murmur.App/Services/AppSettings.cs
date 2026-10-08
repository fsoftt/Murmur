namespace Murmur.App.Services;

/// <summary>Non-secret preferences. The signaling endpoint is configurable so anyone can run their own server.</summary>
public sealed class AppSettings
{
    private const string SignalingKey = "signaling.endpoint";

#if DEBUG
    // The Android emulator reaches the development machine's localhost at 10.0.2.2.
    public const string DefaultSignalingEndpoint = "ws://10.0.2.2:8080/ws";
#else
    public const string DefaultSignalingEndpoint = "wss://signal.murmur.invalid/ws";
#endif

    public Uri SignalingEndpoint
    {
        get => Uri.TryCreate(Preferences.Default.Get(SignalingKey, DefaultSignalingEndpoint), UriKind.Absolute, out var uri) ? uri : new Uri(DefaultSignalingEndpoint);
        set => Preferences.Default.Set(SignalingKey, value.ToString());
    }

    public static bool IsValidEndpoint(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri)
        && (uri.Scheme == "wss" || (uri.Scheme == "ws" && IsDevelopmentBuild));

#if DEBUG
    public const bool IsDevelopmentBuild = true;
#else
    public const bool IsDevelopmentBuild = false;
#endif
}
