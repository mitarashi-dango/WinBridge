using System.Net;
using System.Net.Http;
using System.Text.Json;
using WinBridge.Localization;
using WinBridge.Models;
using WinBridge.Services;
using WinBridge.ViewModels;

internal static class AppUpdateTests
{
    public static Task VersionsAsync()
    {
        foreach (var (tag, expected) in new[]
                 { ("v1.1.5", false), ("1.1.5.0", false), ("v1.1.4", false),
                   ("v1.1.10", true), ("v2.0.0", true), ("v1.1.5.1", true) })
        {
            var result = AppUpdateService.ParseRelease(Release(tag), new Version(1, 1, 5, 0));
            Require(result.UpdateAvailable == expected, $"Incorrect comparison: {tag}");
            Require(ExternalLinkService.CreateUpdatePageStartInfo(result.PageUrl!).UseShellExecute,
                "Release page is not openable.");
        }
        foreach (var tag in new[] { "v1.2.0-beta", "latest", "v1.2.0\n", "v999999999999.0.0" })
        {
            try { AppUpdateService.ParseRelease(Release(tag), new Version(1, 0)); }
            catch (InvalidDataException) { continue; }
            throw new Exception($"Invalid version accepted: {tag}");
        }
        foreach (var flag in new[] { "draft", "prerelease" })
            Require(AppUpdateService.ParseRelease(Release("v9.0.0").Replace(
                $"\"{flag}\":false", $"\"{flag}\":true"), new Version(1, 0)).NoRelease,
                "Unpublished or prerelease version accepted.");
        foreach (var target in new[] { "file:///C:/bad.exe", "https://example.com/",
                     AppUpdateService.ReleasesUrl + "/tag/v1.2.0?x=1",
                     AppUpdateService.ReleasesUrl + "/tag/v1.2.0\n",
                     "ms-windows-store://other", "https://github.com.evil.test/" })
        {
            try { ExternalLinkService.CreateUpdatePageStartInfo(target); }
            catch (ArgumentException) { continue; }
            throw new Exception("Unsafe page accepted.");
        }
        Require(ExternalLinkService.CreateUpdatePageStartInfo(AppUpdateService.StoreUrl).UseShellExecute,
            "Store link is not openable.");
        return Task.CompletedTask;
    }

    public static async Task NetworkAsync()
    {
        using var fixture = new Fixture();
        foreach (var code in new[] { HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests,
                     HttpStatusCode.InternalServerError, HttpStatusCode.Redirect })
        {
            fixture.Handler.Response = () => new(code);
            Require(!(await fixture.Service.CheckAsync()).IsSuccess, "HTTP error accepted.");
        }
        fixture.Handler.Response = () => new(HttpStatusCode.NotFound);
        Require((await fixture.Service.CheckAsync()).NoRelease, "Missing release not handled.");
        fixture.Handler.Response = () => new(HttpStatusCode.OK) { Content = new StringContent("invalid") };
        Require(!(await fixture.Service.CheckAsync()).IsSuccess, "Malformed response accepted.");
        fixture.Handler.Response = () => throw new HttpRequestException("offline");
        Require(!(await fixture.Service.CheckAsync()).IsSuccess, "Offline error not handled.");
        fixture.Handler.Response = () => throw new TaskCanceledException("timeout");
        Require(!(await fixture.Service.CheckAsync()).IsSuccess, "Timeout not handled.");
        fixture.Handler.Response = () => new(HttpStatusCode.OK) { Content = new StringContent(Release("v1.2.0")) };
        Require((await fixture.Service.CheckAsync()).UpdateAvailable, "Retry did not succeed.");
        Require(fixture.Handler.LastUrl == "https://api.github.com/repos/mitarashi-dango/WinBridge/releases/latest"
                && fixture.Handler.HasUserAgent, "Request endpoint or User-Agent missing.");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        try { await fixture.Service.CheckAsync(canceled.Token); }
        catch (OperationCanceledException) { return; }
        throw new Exception("Cancellation was swallowed.");
    }

    public static async Task StoreAsync()
    {
        using var fixture = new Fixture();
        var calls = 0;
        var available = true;
        var service = new AppUpdateService(fixture.Logger, fixture.Client, new Version(1, 0), true,
            _ => { calls++; return Task.FromResult(available); });
        var result = await service.CheckAsync();
        Require(result.UpdateAvailable && result.PageUrl == AppUpdateService.StoreUrl,
            "Store update not routed to Store.");
        available = false;
        Require(!(await service.CheckAsync()).UpdateAvailable, "False Store update.");
        Require(calls == 2 && fixture.Handler.Calls == 0, "Store used GitHub.");
        service = new AppUpdateService(fixture.Logger, fixture.Client, new Version(1, 0), true,
            _ => throw new InvalidOperationException("Store unavailable"));
        Require(!(await service.CheckAsync()).IsSuccess && fixture.Handler.Calls == 0,
            "Store failure fell back to another distribution.");
    }

    public static async Task PreferencesAndNotificationsAsync()
    {
        using var fixture = new Fixture();
        LocalizationService.Initialize("ja-JP");
        Require(JsonSerializer.Deserialize<AppSettings>("{\"Version\":7}")!.CheckForUpdatesOnStartup,
            "Existing settings do not enable update checks.");
        var settingsService = new AppSettingsService(fixture.Logger, fixture.Directory);
        var settings = new AppSettings();
        var modules = new ModuleService(settingsService, settings, fixture.Logger);
        var vm = new AppUpdateViewModel(modules, fixture.Service, new ExternalLinkService(fixture.Logger), _ => { });
        await vm.ToggleAutoCheckAsync();
        Require(!vm.AutoCheck && !(await new AppSettingsService(fixture.Logger, fixture.Directory).LoadAsync())
            .CheckForUpdatesOnStartup, "Preference not persisted.");
        await vm.CheckAutomaticallyAsync();
        Require(fixture.Handler.Calls == 0, "Disabled startup check contacted server.");
        await vm.CheckAsync();
        Require(vm.HasNotification && vm.HasUpdate && !vm.IsChecking, "Manual notification missing.");
        vm.DismissCommand.Execute(null);
        Require(!vm.HasNotification && vm.HasUpdate, "Dismiss lost available update.");
        await vm.CheckAsync();
        Require(vm.HasNotification, "Manual recheck did not redisplay update.");
        fixture.Handler.Response = () => throw new HttpRequestException("offline");
        await vm.CheckAsync();
        Require(vm.HasUpdate && vm.HasNotification && vm.Status.Contains("確認できませんでした"),
            "Failed check lost known update or showed success.");
        fixture.Handler.Response = () => new(HttpStatusCode.OK) { Content = new StringContent(Release("v1.1.5")) };
        await vm.CheckAsync();
        Require(!vm.HasUpdate && !vm.HasNotification, "Stale notification remains.");
        await File.WriteAllTextAsync(Path.Combine(fixture.Directory, "settings.json"), "{\"Version\":999}");
        var readOnlyService = new AppSettingsService(fixture.Logger, fixture.Directory);
        var future = await readOnlyService.LoadAsync();
        vm = new AppUpdateViewModel(new ModuleService(readOnlyService, future, fixture.Logger),
            fixture.Service, new ExternalLinkService(fixture.Logger), _ => { });
        await vm.ToggleAutoCheckAsync();
        Require(vm.AutoCheck, "Failed save did not restore preference.");
    }

    public static async Task ConcurrentAndCanceledAsync()
    {
        using var fixture = new Fixture();
        var completion = new TaskCompletionSource<bool>();
        var calls = 0;
        var service = new AppUpdateService(fixture.Logger, fixture.Client, new Version(1, 0), true,
            async token => { calls++; return await completion.Task.WaitAsync(token); });
        var vm = new AppUpdateViewModel(new ModuleService(new AppSettingsService(fixture.Logger, fixture.Directory),
            new AppSettings(), fixture.Logger), service, new ExternalLinkService(fixture.Logger), _ => { });
        using var cancellation = new CancellationTokenSource();
        var check = vm.CheckAutomaticallyAsync(cancellation.Token);
        Require(vm.IsChecking && !vm.CheckCommand.CanExecute(null), "Check button remains enabled.");
        await vm.CheckAsync();
        Require(calls == 1, "Duplicate request during startup.");
        cancellation.Cancel();
        await check;
        Require(!vm.IsChecking && !vm.HasNotification && vm.CheckCommand.CanExecute(null),
            "Canceled check left stale busy state or notification.");
    }

    private static string Release(string tag) => JsonSerializer.Serialize(new
        { tag_name = tag, draft = false, prerelease = false, html_url = "https://untrusted.example/" });
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private sealed class Fixture : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "WinBridge.UpdateTests." + Guid.NewGuid());
        public LoggingService Logger { get; }
        public FakeHandler Handler { get; } = new();
        public HttpClient Client { get; }
        public AppUpdateService Service { get; }
        public Fixture()
        {
            Logger = new LoggingService(Path.Combine(Directory, "logs"));
            Client = new HttpClient(Handler);
            Service = new AppUpdateService(Logger, Client, new Version(1, 1, 5, 0), false,
                _ => throw new Exception("Unexpected Store call."));
        }
        public void Dispose()
        {
            Client.Dispose();
            System.IO.Directory.Delete(Directory, true);
        }
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? LastUrl { get; private set; }
        public bool HasUserAgent { get; private set; }
        public Func<HttpResponseMessage> Response { get; set; } =
            () => new(HttpStatusCode.OK) { Content = new StringContent(Release("v1.2.0")) };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            LastUrl = request.RequestUri?.AbsoluteUri;
            HasUserAgent = request.Headers.UserAgent.Count > 0;
            token.ThrowIfCancellationRequested();
            return Task.FromResult(Response());
        }
    }
}
