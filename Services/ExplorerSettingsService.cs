using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using WinBridge.Models;

namespace WinBridge.Services;

public sealed record ExplorerSettings(bool ShowFileExtensions, bool ShowHiddenFiles);

public sealed class ExplorerSettingsService
{
    private const string AdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private readonly LoggingService _logger;
    private ExplorerSettings? _undoValue;
    public bool CanChangeSettingsDirectly { get; }

    public ExplorerSettingsService(LoggingService logger, bool? canChangeSettingsDirectly = null)
    {
        _logger = logger;
        CanChangeSettingsDirectly = canChangeSettingsDirectly ?? !PackageIdentityService.IsPackaged;
    }

    public OperationResult<ExplorerSettings> Get()
    {
        if (!CanChangeSettingsDirectly)
            return OperationResult<ExplorerSettings>.Failure(
                "Microsoft Store版では、ファイル表示設定をフォルダー オプションから変更してください。");

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AdvancedKey);
            var hideFileExt = Convert.ToInt32(key?.GetValue("HideFileExt", 1));
            var hidden = Convert.ToInt32(key?.GetValue("Hidden", 2));
            _logger.Info("エクスプローラー表示設定を取得しました。");
            return OperationResult<ExplorerSettings>.Success(new(hideFileExt == 0, hidden == 1));
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

        try
        {
            var before = Get();
            if (!before.IsSuccess) return OperationResult.Failure(before.UserMessage, before.TechnicalDetails);
            _undoValue = before.Value;
            using var key = Registry.CurrentUser.CreateSubKey(AdvancedKey, true);
            key.SetValue("HideFileExt", showExtensions ? 0 : 1, RegistryValueKind.DWord);
            key.SetValue("Hidden", showHidden ? 1 : 2, RegistryValueKind.DWord);
            NotifyShell();
            _logger.Info("エクスプローラー表示設定を変更しました。");
            return OperationResult.Success("ファイル表示設定を変更しました。");
        }
        catch (Exception ex)
        {
            _logger.Error("エクスプローラー表示設定を変更できませんでした。", ex);
            return OperationResult.Failure("ファイル表示設定を変更できませんでした。",
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    public OperationResult Undo()
    {
        if (!CanChangeSettingsDirectly)
            return StorePackageRestriction();

        return _undoValue is null
            ? OperationResult.Failure("元に戻せる変更がありません。")
            : ApplyWithoutUndo(_undoValue);
    }

    private OperationResult ApplyWithoutUndo(ExplorerSettings value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(AdvancedKey, true);
            key.SetValue("HideFileExt", value.ShowFileExtensions ? 0 : 1, RegistryValueKind.DWord);
            key.SetValue("Hidden", value.ShowHiddenFiles ? 1 : 2, RegistryValueKind.DWord);
            NotifyShell();
            _undoValue = null;
            _logger.Info("エクスプローラー表示設定を元に戻しました。");
            return OperationResult.Success("直前のファイル表示設定に戻しました。");
        }
        catch (Exception ex)
        {
            return OperationResult.Failure("元の表示設定に戻せませんでした。",
                $"{ex.GetType().Name}: {ex.Message}");
        }
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

    private static void NotifyShell() =>
        SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

    private static OperationResult StorePackageRestriction() =>
        OperationResult.Failure(
            "Microsoft Store版では、ファイル表示設定をフォルダー オプションから変更してください。");

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
}
