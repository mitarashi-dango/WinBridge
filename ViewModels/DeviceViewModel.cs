using WinBridge.Services;
using WinBridge.Models;

namespace WinBridge.ViewModels;

public sealed class DeviceViewModel : ObservableObject
{
    private readonly DeviceStatusService _deviceStatus;
    private readonly WindowsSettingsLauncher _launcher;
    private readonly Action<OperationResult> _report;
    private string _summary = "デバイスの状態を確認しています…";

    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand OpenCommand { get; }

    public DeviceViewModel(DeviceStatusService deviceStatus,
        WindowsSettingsLauncher launcher, Action<OperationResult> report)
    {
        _deviceStatus = deviceStatus;
        _launcher = launcher;
        _report = report;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        OpenCommand = new RelayCommand(Open);
    }

    public Task RefreshAsync()
    {
        var result = _deviceStatus.GetStatus();
        if (!result.IsSuccess || result.Value is null)
        {
            Summary = result.UserMessage;
            _report(OperationResult.Failure(result.UserMessage, result.TechnicalDetails));
            return Task.CompletedTask;
        }

        Summary = result.Value.ProblemDeviceCount == 0
            ? $"現在接続されているデバイスを確認しました（{result.Value.PresentDeviceCount}件）。問題は報告されていません。"
            : $"問題が報告されているデバイスが {result.Value.ProblemDeviceCount} 件あります。デバイスマネージャーで詳細を確認してください。";
        return Task.CompletedTask;
    }

    private void Open(object? parameter)
    {
        var target = parameter?.ToString() ?? "";
        var result = target == "device-manager"
            ? _launcher.OpenControlPanel("/name Microsoft.DeviceManager")
            : _launcher.Open(target);
        _report(result);
    }
}
