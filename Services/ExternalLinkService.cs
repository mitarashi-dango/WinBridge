using System.Diagnostics;
using WinBridge.Models;

namespace WinBridge.Services;

public sealed class ExternalLinkService
{
    private readonly LoggingService _logger;

    public ExternalLinkService(LoggingService logger) => _logger = logger;

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
