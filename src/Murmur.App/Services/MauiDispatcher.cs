using Murmur.Presentation.Services;

namespace Murmur.App.Services;

public sealed class MauiDispatcher : IUiDispatcher
{
    public void Post(Action action) => MainThread.BeginInvokeOnMainThread(action);
}
