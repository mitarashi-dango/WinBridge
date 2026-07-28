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
            Start(CreateSettingsStartInfo(target));
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

    public OperationResult OpenControlPanel(params string[] arguments)
    {
        try
        {
            Start(CreateControlPanelStartInfo(arguments));
            _logger.Info("コントロールパネル項目を開きました。");
            return OperationResult.Success("Windowsの画面を開きました。");
        }
        catch (Exception ex)
        {
            _logger.Error("コントロールパネル項目を開けませんでした。", ex);
            return OperationResult.Failure("Windowsの画面を開けませんでした。", $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    public OperationResult OpenDeviceManager()
    {
        try
        {
            Start(CreateDeviceManagerStartInfo());
            _logger.Info("デバイスマネージャーを開きました。");
            return OperationResult.Success("デバイスマネージャーを開きました。");
        }
        catch (Exception ex)
        {
            _logger.Error("デバイスマネージャーを開けませんでした。", ex);
            return OperationResult.Failure(
                "デバイスマネージャーを開けませんでした。",
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    public OperationResult OpenFolderOptions()
    {
        try
        {
            Start(CreateFolderOptionsStartInfo());
            _logger.Info("フォルダー オプションを開きました。");
            return OperationResult.Success("フォルダー オプションを開きました。");
        }
        catch (Exception ex)
        {
            _logger.Error("フォルダー オプションを開けませんでした。", ex);
            return OperationResult.Failure(
                "フォルダー オプションを開けませんでした。",
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static ProcessStartInfo CreateSettingsStartInfo(string target)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) ||
            !uri.Scheme.Equals("ms-settings", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("許可されていないWindows設定URIです。", nameof(target));

        return new ProcessStartInfo(target) { UseShellExecute = true };
    }

    internal static ProcessStartInfo CreateControlPanelStartInfo(params string[] arguments)
    {
        var info = CreateSystemProcessStartInfo("control.exe");
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);
        return info;
    }

    internal static ProcessStartInfo CreateDeviceManagerStartInfo()
    {
        var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var info = CreateSystemProcessStartInfo("mmc.exe");
        info.ArgumentList.Add(Path.Combine(systemDirectory, "devmgmt.msc"));
        return info;
    }

    internal static ProcessStartInfo CreateFolderOptionsStartInfo()
    {
        var info = CreateSystemProcessStartInfo("rundll32.exe");
        info.ArgumentList.Add("shell32.dll,Options_RunDLL");
        info.ArgumentList.Add("0");
        return info;
    }

    private static ProcessStartInfo CreateSystemProcessStartInfo(string fileName)
    {
        var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        return new ProcessStartInfo(Path.Combine(systemDirectory, fileName))
        {
            UseShellExecute = true
        };
    }

    private static void Start(ProcessStartInfo info)
    {
        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("Windowsプロセスを開始できませんでした。");
    }

    private static string SafeTarget(string target) =>
        target.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase) ? target : "[standard Windows target]";
}
