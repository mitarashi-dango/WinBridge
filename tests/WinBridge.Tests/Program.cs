using System.Text.Json;
using WinBridge.Models;
using WinBridge.Services;

var tests = new (string Name, Func<Task> Run)[]
{
    ("設定Versionを段階的に移行できる", TestMigrationAsync),
    ("新しいVersionを誤って上書きしない", TestFutureVersionAsync),
    ("同時保存でも有効なJSONを維持する", TestConcurrentSaveAsync),
    ("破損時に前回バックアップから復旧する", TestBackupRecoveryAsync),
    ("設定カタログが安全なURIだけを持つ", TestCatalogSafetyAsync),
    ("Windowsコマンドをタイムアウトできる", TestCommandTimeoutAsync),
    ("2個目の起動から既存起動へ通知できる", TestSingleInstanceAsync)
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.Message}");
        Console.WriteLine($"FAIL  {test.Name}");
    }
}

Console.WriteLine();
Console.WriteLine($"{tests.Length - failures.Count}/{tests.Length} tests passed");
if (failures.Count > 0)
{
    foreach (var failure in failures) Console.WriteLine($"  {failure}");
    return 1;
}
return 0;

static Task TestMigrationAsync()
{
    var settings = new AppSettings { Version = 1, Settings = [] };
    var result = AppSettingsMigrator.Migrate(settings);
    Assert(result.CanSave, "移行後に保存できません。");
    Assert(result.Settings.Version == AppSettingsMigrator.CurrentVersion, "現在Versionへ移行されません。");
    Assert(result.Settings.Settings.Count == 3, "初期Windows設定が移行されません。");
    Assert(result.Settings.Settings.All(s => s.IsPinned), "移行設定が左メニューへ固定されません。");
    return Task.CompletedTask;
}

static Task TestFutureVersionAsync()
{
    var result = AppSettingsMigrator.Migrate(new AppSettings { Version = 999 });
    Assert(!result.CanSave, "未来Versionを保存可能として扱っています。");
    Assert(result.Settings.Version == 999, "未来Versionを書き換えています。");
    return Task.CompletedTask;
}

static async Task TestConcurrentSaveAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var logger = new LoggingService(Path.Combine(directory, "logs"));
        var service = new AppSettingsService(logger, directory);
        var tasks = Enumerable.Range(0, 30).Select(index =>
            service.SaveAsync(new AppSettings
            {
                Version = AppSettingsMigrator.CurrentVersion,
                LastModuleId = $"module-{index}",
                Settings =
                [
                    new SettingPreference
                    {
                        Id = "system.display",
                        Order = 1,
                        IsFavorite = index % 2 == 0,
                        IsPinned = true
                    }
                ]
            }));
        var results = await Task.WhenAll(tasks);
        Assert(results.All(r => r.IsSuccess), "同時保存の一部が失敗しました。");

        var json = await File.ReadAllTextAsync(Path.Combine(directory, "settings.json"));
        var loaded = JsonSerializer.Deserialize<AppSettings>(json);
        if (loaded is null) throw new InvalidOperationException("保存後のJSONを読み込めません。");
        Assert(loaded.Settings.Count == 1, "保存内容が欠落しています。");
        Assert(!Directory.EnumerateFiles(directory, "settings.*.tmp").Any(), "一時ファイルが残っています。");
    }
    finally { DeleteTemporaryDirectory(directory); }
}

static async Task TestBackupRecoveryAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var logger = new LoggingService(Path.Combine(directory, "logs"));
        var service = new AppSettingsService(logger, directory);
        var first = new AppSettings { LastModuleId = "first" };
        var second = new AppSettings { LastModuleId = "second" };
        Assert((await service.SaveAsync(first)).IsSuccess, "最初の保存に失敗しました。");
        Assert((await service.SaveAsync(second)).IsSuccess, "2回目の保存に失敗しました。");
        await File.WriteAllTextAsync(Path.Combine(directory, "settings.json"), "{ broken json");

        var restored = await new AppSettingsService(logger, directory).LoadAsync();
        Assert(restored.LastModuleId == "first", "前回バックアップから復旧できません。");
        Assert(Directory.EnumerateFiles(directory, "settings.broken-*.json").Any(),
            "破損ファイルが退避されていません。");
    }
    finally { DeleteTemporaryDirectory(directory); }
}

static async Task TestCatalogSafetyAsync()
{
    var path = Path.Combine(AppContext.BaseDirectory, "Resources", "SettingDefinitions.json");
    var json = await File.ReadAllTextAsync(path);
    var settings = JsonSerializer.Deserialize<List<SettingDefinition>>(json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    Assert(settings.Count >= 40, "設定カタログの項目数が想定より少なくなっています。");
    Assert(settings.Select(s => s.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == settings.Count,
        "設定カタログに重複IDがあります。");
    Assert(settings.All(s => Uri.TryCreate(s.Target, UriKind.Absolute, out var uri)
                             && uri.Scheme.Equals("ms-settings", StringComparison.OrdinalIgnoreCase)),
        "安全でない設定URIが含まれています。");
}

static async Task TestCommandTimeoutAsync()
{
    var result = await CommandRunner.RunAsync(
        "ping.exe", TimeSpan.FromMilliseconds(150), "-n", "6", "127.0.0.1");
    Assert(!result.IsSuccess, "長時間コマンドが成功扱いになっています。");
    Assert(result.TechnicalDetails?.Contains("タイムアウト", StringComparison.Ordinal) == true,
        "タイムアウトの技術情報がありません。");
}

static async Task TestSingleInstanceAsync()
{
    using var first = new SingleInstanceService();
    Assert(first.IsFirstInstance, "最初のインスタンスが所有権を取得できません。");
    var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    first.ListenForActivation(() => activated.TrySetResult());
    using var second = new SingleInstanceService();
    Assert(!second.IsFirstInstance, "2個目のインスタンスが起動可能になっています。");
    second.SignalExistingInstance();
    await activated.Task.WaitAsync(TimeSpan.FromSeconds(2));
}

static string CreateTemporaryDirectory()
{
    var directory = Path.Combine(Path.GetTempPath(), $"WinBridge.Tests.{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    return directory;
}

static void DeleteTemporaryDirectory(string directory)
{
    try { Directory.Delete(directory, true); }
    catch { }
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
