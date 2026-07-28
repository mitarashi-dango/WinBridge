using System.Windows.Input;

namespace WinBridge.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute()) { }
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        => (_execute, _canExecute) = (execute, canExecute);
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private bool _isRunning;
    public AsyncRelayCommand(Func<Task> execute) => _execute = execute;
    public bool CanExecute(object? parameter) => !_isRunning;
    public async void Execute(object? parameter)
    {
        if (_isRunning) return;
        _isRunning = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await _execute(); }
        finally
        {
            _isRunning = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public event EventHandler? CanExecuteChanged;
}
