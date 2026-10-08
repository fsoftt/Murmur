using Android.App;
using Android.Runtime;

namespace Directo.App;

#if DEBUG
// Development builds may talk to a local signaling server over ws://.
[Application(UsesCleartextTraffic = true)]
#else
[Application]
#endif
public class MainApplication(IntPtr handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
