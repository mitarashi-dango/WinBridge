using System.Diagnostics;
using WinBridge.Models;

namespace WinBridge.Services;

public sealed record ExplorerSettings(bool ShowFileExtensions, bool ShowHiddenFiles);

public sealed class ExplorerSettingsService
{
    private readonly LoggingService _logger;
    private readonly IExplorerSettingsAccessor _accessor;
    private ExplorerSettingsSnapshot? _undoValue;
    public bool CanChangeSettingsDirectly { get; }

    public ExplorerSettingsService(LoggingService logger, bool? canChangeSettingsDirectly = null)
        : this(logger, new ExplorerSettingsAccessor(),
            canChangeSettingsDirectly ?? !PackageIdentityService.IsPackaged) { }

    internal ExplorerSettingsService(LoggingService logger, IExplorerSettingsAccessor accessor,
        bool canChangeSettingsDirectly = true)
    {
        _logger = logger;
        _accessor = accessor;
        CanChangeSettingsDirectly = canChangeSettingsDirectly;
    }

    public OperationResult<ExplorerSettings> Get()
    {
        if (!CanChangeSettingsDirectly)
            return OperationResult<ExplorerSettings>.Failure(
                "Microsoft Store版では、ファイル表示設定をフォルダー オプションから変更してください。");

        try
        {
            var settings = _accessor.Read().ToSettings();
            _logger.Info("エクスプローラー表示設定を取得しました。");
            return OperationResult<ExplorerSettings>.Success(settings);
        }
        catch (Exception ex)
        {
            _logger.Error("エクスプローラー表示設定を取得できませんでした。", ex);
            return OperationResult<ExplorerSettings>.Failure("ファイル表示設定を取得できませんでした。",
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    public OperationResult Apply(bool showExtensions, bool showHidden)
    {
        if (!CanChangeSettingsDirectly)
            return StorePackageRestriction();

        return Change(ExplorerSettingsSnapshot.FromSettings(showExtensions, showHidden), isUndo: false);
    }

    public OperationResult Undo()
    {
        if (!CanChangeSettingsDirectly)
            return StorePackageRestriction();

        return _undoValue is null
            ? OperationResult.Failure("元に戻せる変更がありません。")
            : Change(_undoValue, isUndo: true);
    }

    private OperationResult Change(ExplorerSettingsSnapshot value, bool isUndo)
    {
        ExplorerSettingsSnapshot before;
        try
        {
            before = _accessor.Read();
            _ = before.ToSettings();
        }
        catch (Exception ex)
        {
            _logger.Error("エクスプローラー表示設定を取得できませんでした。", ex);
            return OperationResult.Failure("ファイル表示設定を取得できませんでした。",
                $"{ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            _accessor.WriteValue(ExplorerValue.HideFileExt, value.HideFileExt);
            _accessor.WriteValue(ExplorerValue.Hidden, value.Hidden);
            _accessor.NotifyShell();
            Verify(value);
            // 成功した変更だけを「元に戻す」の対象にする。
            _undoValue = isUndo ? null : before;
            var message = isUndo ? "直前のファイル表示設定に戻しました。" : "ファイル表示設定を変更しました。";
            _logger.Info(message);
            return OperationResult.Success(message);
        }
        catch (Exception ex)
        {
            var message = isUndo ? "元の表示設定に戻せませんでした。" : "ファイル表示設定を変更できませんでした。";
            _logger.Error(message, ex);
            var rollbackErrors = Restore(before);
            var details = $"{ex.GetType().Name}: {ex.Message}";
            if (rollbackErrors.Count == 0)
                return OperationResult.Failure($"{L.T(message)} {L.T("変更前の値へ戻しました。")}", details);

            // 復元が不完全だった場合、適用直前の値への復元を再試行できるよう保持する。
            if (!isUndo) _undoValue = before;
            _logger.Error("ファイル表示設定を完全には元へ戻せませんでした。");
            return OperationResult.Failure(
                "ファイル表示設定の変更に失敗し、元の値へ完全には戻せませんでした。フォルダー オプションで確認してください。",
                $"{details}; Rollback: {string.Join("; ", rollbackErrors)}");
        }
    }

    private List<string> Restore(ExplorerSettingsSnapshot original)
    {
        var errors = new List<string>();
        // 一方の復元に失敗しても、もう一方の復元と確認を試みる。
        Attempt(() => _accessor.WriteValue(ExplorerValue.HideFileExt, original.HideFileExt));
        Attempt(() => _accessor.WriteValue(ExplorerValue.Hidden, original.Hidden));
        Attempt(_accessor.NotifyShell);
        Attempt(() => Verify(original));
        return errors;

        void Attempt(Action action)
        {
            try { action(); }
            catch (Exception ex) { errors.Add($"{ex.GetType().Name}: {ex.Message}"); }
        }
    }

    private void Verify(ExplorerSettingsSnapshot expected)
    {
        if (_accessor.Read() != expected)
            throw new InvalidOperationException("File display settings verification failed.");
    }

    public async Task<OperationResult> RestartExplorerAsync()
    {
        var stop = await CommandRunner.RunAsync("taskkill.exe", "/F", "/IM", "explorer.exe");
        await Task.Delay(800);
        try
        {
            var explorerPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            Process.Start(new ProcessStartInfo(explorerPath) { UseShellExecute = true });
            _logger.Info($"エクスプローラーを再起動しました。終了処理成功: {stop.IsSuccess}");
            return OperationResult.Success("エクスプローラーを再起動しました。");
        }
        catch (Exception ex)
        {
            _logger.Error("エクスプローラーを再起動できませんでした。", ex);
            return OperationResult.Failure(
                "エクスプローラーを再起動できませんでした。タスク マネージャーから explorer.exe を起動してください。",
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static OperationResult StorePackageRestriction() =>
        OperationResult.Failure(
            "Microsoft Store版では、ファイル表示設定をフォルダー オプションから変更してください。");

}
