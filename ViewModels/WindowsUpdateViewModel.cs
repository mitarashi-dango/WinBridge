using WinBridge.Services;
using WinBridge.Models;

namespace WinBridge.ViewModels;

public sealed class WindowsUpdateViewModel : ObservableObject
{
    private readonly WindowsSettingsLauncher _launcher;
    private readonly WindowsUpdateStatusService _statusService;
    private readonly Action<OperationResult> _report;
    private string _updateStatus = "状態を確認しています…";
    public string UpdateStatus { get => _updateStatus; set => SetProperty(ref _updateStatus, value); }
    public RelayCommand OpenCommand { get; }

    public WindowsUpdateViewModel(WindowsSettingsLauncher launcher,
        WindowsUpdateStatusService statusService, Action<OperationResult> report)
    {
        _launcher = launcher;
        _statusService = statusService;
        _report = report;
        OpenCommand = new RelayCommand(p => Open(p?.ToString() ?? "ms-settings:windowsupdate"));
    }

    public void Refresh() => UpdateStatus = _statusService.GetStatus();
    private void Open(string uri) => _report(_launcher.Open(uri));
}
