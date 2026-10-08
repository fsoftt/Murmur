using CommunityToolkit.Mvvm.ComponentModel;
using Directo.Domain.Model;
using Directo.Presentation.Formatting;

namespace Directo.Presentation.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Runs an action, turning domain failures into a user-facing message instead of a crash.</summary>
    protected async Task RunAsync(Func<Task> action)
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            await action();
        }
        catch (DirectoException ex)
        {
            ErrorMessage = UserMessages.ForError(ex.Code);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsBusy = false;
        }
    }
}
