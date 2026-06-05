using System.Windows.Input;

namespace UnifiedMessenger.Services;

internal sealed class TrayActionCommand : ICommand
{
    private readonly Action _action;

    public TrayActionCommand(Action action) => _action = action;

#pragma warning disable CS0067 // ICommand requires CanExecuteChanged; tray actions are always executable.
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _action();
}
