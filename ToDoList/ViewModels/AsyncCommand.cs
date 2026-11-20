using System.Windows.Input;

namespace ToDoList.ViewModels;

public sealed class AsyncCommand : ICommand
{
    private readonly Func<object?, Task> execute;
    private readonly Action<Exception> onError;
    private readonly Func<object?, bool>? canExecute;
    private bool running;

    public AsyncCommand(Func<Task> execute, Action<Exception> onError)
        : this(_ => execute(), onError)
    {
    }

    public AsyncCommand(Func<object?, Task> execute, Action<Exception> onError,
        Func<object?, bool>? canExecute = null)
    {
        this.execute = execute;
        this.onError = onError;
        this.canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !running && (canExecute?.Invoke(parameter) ?? true);

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    public async void Execute(object? parameter) => await ExecuteAsync(parameter);

    public async Task ExecuteAsync(object? parameter = null)
    {
        if (!CanExecute(parameter)) return;
        running = true;
        NotifyCanExecuteChanged();
        try { await execute(parameter); }
        catch (Exception error) { onError(error); }
        finally
        {
            running = false;
            NotifyCanExecuteChanged();
        }
    }
}
