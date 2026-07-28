using System.Diagnostics;
using System.Windows;
using WinBridge.Models;
using WinBridge.Services;

namespace WinBridge.ViewModels;

public sealed class ExplorerViewModel : ObservableObject
{
    private readonly ExplorerSettingsService _service;
    private readonly WindowsSettingsLauncher _launcher;
    private readonly Action<OperationResult> _report;
    private bool _showExtensions, _showHidden;
    public bool ShowExtensions { get => _showExtensions; set => SetProperty(ref _showExtensions, value); }
    public bool ShowHidden { get => _showHidden; set => SetProperty(ref _showHidden, value); }
    public RelayCommand OpenCommand { get; }
    public RelayCommand ApplyCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand UndoCommand { get; }
    public AsyncRelayCommand RestartCommand { get; }

    public ExplorerViewModel(ExplorerSettingsService service, WindowsSettingsLauncher launcher,
        Action<OperationResult> report)
    {
        _service = service; _launcher = launcher; _report = report;
        OpenCommand = new RelayCommand(Open);
        ApplyCommand = new RelayCommand(Apply);
        RefreshCommand = new RelayCommand(Refresh);
        UndoCommand = new RelayCommand(() => { var r = _service.Undo(); _report(r); if (r.IsSuccess) Refresh(); });
        RestartCommand = new AsyncRelayCommand(RestartAsync);
    }

    public void Refresh()
    {
        var result = _service.Get();
        if (result.IsSuccess && result.Value is not null)
        {
            ShowExtensions = result.Value.ShowFileExtensions;
            ShowHidden = result.Value.ShowHiddenFiles;
        }
        _report(result.IsSuccess
            ? OperationResult.Success("現在のファイル表示設定を読み込みました。")
            : OperationResult.Failure(result.UserMessage, result.TechnicalDetails));
    }

    private void Apply()
    {
        var result = _service.Apply(ShowExtensions, ShowHidden);
        _report(result);
        if (result.IsSuccess) Refresh();
    }

    private void Open(object? parameter)
    {
        var target = parameter?.ToString() ?? "";
        if (target == "explorer")
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
                _report(OperationResult.Success("エクスプローラーを開きました。"));
            }
            catch (Exception ex)
            {
                _report(OperationResult.Failure("エクスプローラーを開けませんでした。",
                    $"{ex.GetType().Name}: {ex.Message}"));
            }
        }
        else if (target == "folders") _report(_launcher.OpenFolderOptions());
        else _report(_launcher.Open(target));
    }

    private async Task RestartAsync()
    {
        var answer = MessageBox.Show(
            L.T("エクスプローラーを再起動します。\n\nタスクバーやデスクトップが一時的に消え、数秒後に再表示されます。\n\n実行しますか？"),
            L.T("再起動の確認"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        _report(await _service.RestartExplorerAsync());
    }
}
