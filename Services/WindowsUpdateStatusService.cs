using Microsoft.Win32;

namespace WinBridge.Services;

public sealed class WindowsUpdateStatusService
{
    private readonly LoggingService _logger;
    public WindowsUpdateStatusService(LoggingService logger) => _logger = logger;

    public string GetStatus()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
            var status = key is null
                ? "状態：Windowsの設定画面で確認してください"
                : "状態：更新後の再起動が必要な可能性があります";
            _logger.Info("Windows Updateの再起動待ち状態を確認しました。");
            return L.T(status);
        }
        catch
        {
            return L.T("状態：Windowsの設定画面で確認してください");
        }
    }
}
