using WinBridge.Models;
using WinBridge.Services;

namespace WinBridge.ViewModels;

public sealed class AppUpdateViewModel : ObservableObject
{
    private readonly ModuleService _modules;
    private readonly AppUpdateService _service;
    private readonly ExternalLinkService _links;
    private readonly Action<OperationResult> _report;
    private bool _isChecking;
    private bool _hasNotification;
    private string _status = L.T("更新はまだ確認していません。");
    private string _notification = "";
    private string? _pageUrl;
    public bool AutoCheck => _modules.Settings.CheckForUpdatesOnStartup;
    public bool IsChecking { get => _isChecking; private set => SetProperty(ref _isChecking, value); }
    public bool HasNotification { get => _hasNotification; private set => SetProperty(ref _hasNotification, value); }
    public bool HasUpdate => _pageUrl is not null;
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string Notification { get => _notification; private set => SetProperty(ref _notification, value); }
    public string SourceDescription => _service.IsStore
        ? L.T("Microsoft Storeで更新を確認します。更新はStoreからインストールできます。")
        : L.T("GitHubで正式リリースを確認します。更新は配布ページからダウンロードできます。");
    public AsyncRelayCommand CheckCommand { get; }
    public AsyncRelayCommand ToggleAutoCheckCommand { get; }
    public RelayCommand OpenUpdateCommand { get; }
    public RelayCommand DismissCommand { get; }

    public AppUpdateViewModel(ModuleService modules, AppUpdateService service,
        ExternalLinkService links, Action<OperationResult> report)
    {
        _modules = modules;
        _service = service;
        _links = links;
        _report = report;
        CheckCommand = new AsyncRelayCommand(() => CheckAsync(), () => !IsChecking);
        ToggleAutoCheckCommand = new AsyncRelayCommand(ToggleAutoCheckAsync);
        OpenUpdateCommand = new RelayCommand(() =>
        {
            if (_pageUrl is not null) _report(_links.OpenUpdatePage(_pageUrl));
        }, () => HasUpdate);
        DismissCommand = new RelayCommand(() => HasNotification = false);
    }

    public Task CheckAutomaticallyAsync(CancellationToken token = default) =>
        AutoCheck ? CheckAsync(token) : Task.CompletedTask;

    internal async Task ToggleAutoCheckAsync()
    {
        var previous = AutoCheck;
        _modules.Settings.CheckForUpdatesOnStartup = !previous;
        OnPropertyChanged(nameof(AutoCheck));
        var result = await _modules.SaveAsync();
        if (!result.IsSuccess)
        {
            _modules.Settings.CheckForUpdatesOnStartup = previous;
            OnPropertyChanged(nameof(AutoCheck));
        }
        _report(result);
    }

    public async Task CheckAsync(CancellationToken token = default)
    {
        if (IsChecking) return;
        IsChecking = true;
        CheckCommand.RaiseCanExecuteChanged();
        var previousStatus = Status;
        Status = L.T("更新を確認しています…");
        try
        {
            var result = await _service.CheckAsync(token);
            token.ThrowIfCancellationRequested();
            if (!result.IsSuccess)
            {
                Status = L.T("更新を確認できませんでした。接続を確認して、もう一度お試しください。");
                return;
            }
            _pageUrl = result.UpdateAvailable ? result.PageUrl : null;
            OnPropertyChanged(nameof(HasUpdate));
            OpenUpdateCommand.RaiseCanExecuteChanged();
            HasNotification = result.UpdateAvailable;
            Status = result.UpdateAvailable
                ? (_service.IsStore ? L.T("Microsoft StoreにWinBridgeの更新があります。")
                    : L.F("WinBridge {0} が公開されています。", result.Version))
                : result.NoRelease ? L.T("公開済みの正式リリースがありません。")
                : L.T("利用できる更新はありません。");
            Notification = result.UpdateAvailable ? Status : "";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Status = previousStatus;
        }
        finally
        {
            IsChecking = false;
            CheckCommand.RaiseCanExecuteChanged();
        }
    }
}
