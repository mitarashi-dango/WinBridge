using WinBridge.Services;
using WinBridge.Models;

namespace WinBridge.ViewModels;

public sealed class SearchViewModel : ObservableObject
{
    private readonly WindowsSettingsLauncher _launcher;
    private readonly SearchStatusService _statusService;
    private readonly Action<OperationResult> _report;
    private string _serviceStatus = L.T("状態を確認しています…");
    private string _guidance = L.T("問題を選ぶと、ここに安全な確認手順を表示します。");
    public string ServiceStatus { get => _serviceStatus; set => SetProperty(ref _serviceStatus, value); }
    public string Guidance { get => _guidance; set => SetProperty(ref _guidance, value); }
    public RelayCommand OpenCommand { get; }
    public RelayCommand GuidanceCommand { get; }

    public SearchViewModel(WindowsSettingsLauncher launcher, SearchStatusService statusService,
        Action<OperationResult> report)
    {
        _launcher = launcher; _statusService = statusService; _report = report;
        OpenCommand = new RelayCommand(Open);
        GuidanceCommand = new RelayCommand(p => ShowGuidance(p?.ToString() ?? ""));
    }

    public async Task RefreshAsync()
    {
        var result = await _statusService.GetStatusAsync();
        ServiceStatus = result.IsSuccess && result.Value is not null
            ? result.Value
            : result.UserMessage;
        if (!result.IsSuccess)
            _report(OperationResult.Failure(result.UserMessage, result.TechnicalDetails));
    }

    private void Open(object? parameter)
    {
        var target = parameter?.ToString() ?? "";
        var result = target == "indexing"
            ? _launcher.OpenControlPanel("/name", "Microsoft.IndexingOptions")
            : _launcher.Open(target);
        _report(result);
    }

    private void ShowGuidance(string issue) => Guidance = L.T(issue switch
    {
        "missing" => "1. 検索対象フォルダーを確認します。\n2. インデックス設定で対象の場所を確認します。\n3. ファイルが同期中でないか確認します。",
        "old" => "1. インデックスの状態を確認します。\n2. 対象フォルダーが登録されているか確認します。\n3. 必要な場合だけ、詳細設定から再構築を選びます。",
        "slow" => "1. インデックス作成が完了しているか確認します。\n2. 検索対象が広すぎないか確認します。\n3. Windows Update後なら、しばらく待って再確認します。",
        "local" => "Windows検索設定の「クラウド コンテンツの検索」と検索対象を確認してください。WinBridgeはポリシーやレジストリを変更しません。",
        _ => "スタート設定を開き、最近追加したアプリやおすすめ項目の表示を調整できます。"
    });
}
