using Directo.Presentation.Services;

namespace Directo.App.Services;

public sealed class MauiDispatcher : IUiDispatcher
{
    public void Post(Action action) => MainThread.BeginInvokeOnMainThread(action);
}
