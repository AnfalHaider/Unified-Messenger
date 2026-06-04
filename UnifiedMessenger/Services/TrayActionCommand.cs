using System.Windows.Input;

namespace UnifiedMessenger.Services;

internal sealed class TrayActionCommand : ICommand
{
    private readonly Action _action;

    public TrayActionCommand(Action action) => _action = action;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _action();
}
