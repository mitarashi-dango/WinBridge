using System.Diagnostics;
using WinBridge.Models;

namespace WinBridge.Services;

public sealed class WindowsSettingsLauncher
{
    private readonly LoggingService _logger;
    public WindowsSettingsLauncher(LoggingService logger) => _logger = logger;

    public OperationResult Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
            _logger.Info($"Windows画面を開きました: {SafeTarget(target)}");
            return OperationResult.Success("Windowsの画面を開きました。");
        }
        catch (Exception ex)
        {
            _logger.Error($"Windows画面を開けませんでした: {SafeTarget(target)}", ex);
            return OperationResult.Failure(
                "Windowsの設定画面を開けませんでした。Windowsのバージョンによって利用できない可能性があります。",
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    public OperationResult OpenControlPanel(string arguments)
    {
        try
        {
            var info = new ProcessStartInfo("control.exe") { UseShellExecute = true };
            info.ArgumentList.Add(arguments);
            Process.Start(info);
            _logger.Info("コントロールパネル項目を開きました。");
            return OperationResult.Success("Windowsの画面を開きました。");
        }
        catch (Exception ex)
        {
            _logger.Error("コントロールパネル項目を開けませんでした。", ex);
            return OperationResult.Failure("Windowsの画面を開けませんでした。", $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string SafeTarget(string target) =>
        target.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase) ? target : "[standard Windows target]";
}
