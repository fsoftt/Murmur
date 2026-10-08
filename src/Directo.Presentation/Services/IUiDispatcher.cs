namespace Directo.Presentation.Services;

/// <summary>Runs work on the UI thread. Domain events arrive on background threads.</summary>
public interface IUiDispatcher
{
    void Post(Action action);
}

/// <summary>Runs inline; for tests and non-UI hosts.</summary>
public sealed class InlineDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}
