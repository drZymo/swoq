using System.Windows.Input;

namespace MapGeneratorTester.ViewModels;

internal class RelayCommand(Action<object?>? execute = null, Func<object?, bool>? canExecute = null) : ICommand
{
    private readonly Func<object?, bool>? canExecute = canExecute;
    private readonly Action<object?>? execute = execute;

    public bool CanExecute(object? parameter)
    {
        return canExecute == null || canExecute.Invoke(parameter);
    }

    public void Execute(object? parameter)
    {
        execute?.Invoke(parameter);
    }

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
