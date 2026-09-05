using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Windows.Services.Store;

namespace WinBridge.Services;

public sealed record AppUpdateResult(bool IsSuccess, bool UpdateAvailable = false,
    string? Version = null, string? PageUrl = null, bool NoRelease = false);

public sealed class AppUpdateService
{
    internal const string ReleasesUrl = "https://github.com/mitarashi-dango/WinBridge/releases";
    internal const string StoreUrl = "ms-windows-store://downloadsandupdates";
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(15),
        MaxResponseContentBufferSize = 1024 * 1024
    };
    private readonly LoggingService _logger;
    private readonly HttpClient _client;
    private readonly Version _currentVersion;
    private readonly Func<CancellationToken, Task<bool>> _checkStore;
    public bool IsStore { get; }

    public AppUpdateService(LoggingService logger)
        : this(logger, Client, typeof(AppUpdateService).Assembly.GetName().Version!,
            PackageIdentityService.IsPackaged, CheckStoreAsync) { }

    internal AppUpdateService(LoggingService logger, HttpClient client, Version currentVersion,
        bool isStore, Func<CancellationToken, Task<bool>> checkStore)
    {
        _logger = logger;
        _client = client;
        _currentVersion = Normalize(currentVersion);
        IsStore = isStore;
        _checkStore = checkStore;
    }

    public async Task<AppUpdateResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            if (IsStore)
                return new(true, await _checkStore(timeout.Token), PageUrl: StoreUrl);

            using var request = new HttpRequestMessage(HttpMethod.Get,
                "https://api.github.com/repos/mitarashi-dango/WinBridge/releases/latest");
            request.Headers.UserAgent.ParseAdd($"WinBridge/{_currentVersion}");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await _client.SendAsync(request, timeout.Token);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new(true, NoRelease: true);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(timeout.Token);
            return ParseRelease(json, _currentVersion);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error("WinBridgeの更新確認に失敗しました。", ex);
            return new(false);
        }
    }

    internal static AppUpdateResult ParseRelease(string json, Version currentVersion)
    {
        using var document = JsonDocument.Parse(json);
        var release = document.RootElement;
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean())
            return new(true, NoRelease: true);
        var tag = release.GetProperty("tag_name").GetString() ?? "";
        if (!Regex.IsMatch(tag, @"\Av?\d+\.\d+\.\d+(\.\d+)?\z", RegexOptions.CultureInvariant) ||
            !Version.TryParse(tag.TrimStart('v'), out var version))
            throw new InvalidDataException("Unsupported release version.");
        var url = ReleasesUrl + "/tag/" + Uri.EscapeDataString(tag);
        return new(true, Normalize(version) > Normalize(currentVersion), tag, url);
    }

    private static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));

    private static async Task<bool> CheckStoreAsync(CancellationToken token)
    {
        var context = StoreContext.GetDefault();
        var window = System.Windows.Application.Current?.MainWindow;
        if (window is not null)
            WinRT.Interop.InitializeWithWindow.Initialize(context,
                new System.Windows.Interop.WindowInteropHelper(window).Handle);
        var updates = await context.GetAppAndOptionalStorePackageUpdatesAsync().AsTask(token);
        return updates.Count > 0;
    }
}
