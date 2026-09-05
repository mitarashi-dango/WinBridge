using System.Diagnostics;
using WinBridge.Models;

namespace WinBridge.Services;

public sealed class ExternalLinkService
{
    private readonly LoggingService _logger;

    public ExternalLinkService(LoggingService logger) => _logger = logger;

    public OperationResult OpenEverythingPage()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("https://www.voidtools.com/")
                { UseShellExecute = true })
                ?? throw new InvalidOperationException("Could not start browser.");
            return OperationResult.Success("Everythingの公式サイトを開きました。");
        }
        catch (Exception ex)
        {
            _logger.Error("Everythingの公式サイトを開けませんでした。", ex);
            return OperationResult.Failure("Everythingの公式サイトを開けませんでした。", ex.Message);
        }
    }

    public OperationResult OpenUpdatePage(string target)
    {
        try
        {
            using var process = Process.Start(CreateUpdatePageStartInfo(target))
                ?? throw new InvalidOperationException("Could not start update page.");
            return OperationResult.Success("更新ページを開きました。");
        }
        catch (Exception ex)
        {
            _logger.Error("更新ページを開けませんでした。", ex);
            return OperationResult.Failure("更新ページを開けませんでした。", ex.Message);
        }
    }

    internal static ProcessStartInfo CreateUpdatePageStartInfo(string target)
    {
        if (target != AppUpdateService.StoreUrl &&
            !System.Text.RegularExpressions.Regex.IsMatch(target,
                @"\Ahttps://github\.com/mitarashi-dango/WinBridge/releases/tag/v?\d+\.\d+\.\d+(\.\d+)?\z"))
            throw new ArgumentException("Invalid update page.", nameof(target));
        return new ProcessStartInfo(target) { UseShellExecute = true };
    }

    public OperationResult OpenSupportPage()
    {
        const string supportUrl = "https://ko-fi.com/nioudachi";
        try
        {
            using var process = Process.Start(CreateSupportPageStartInfo(supportUrl))
                ?? throw new InvalidOperationException("ブラウザーを開始できませんでした。");
            _logger.Info("開発支援ページをブラウザーで開きました。");
            return OperationResult.Success("開発支援ページをブラウザーで開きました。");
        }
        catch (Exception ex)
        {
            _logger.Error("開発支援ページを開けませんでした。", ex);
            return OperationResult.Failure(
                "開発支援ページを開けませんでした。ブラウザーの設定を確認してください。",
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static ProcessStartInfo CreateSupportPageStartInfo(string target)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) ||
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !uri.Host.Equals("ko-fi.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.Equals("/nioudachi", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("許可されていない支援ページです。", nameof(target));

        return new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true };
    }
}
