using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using WinBridge.Models;
using WinBridge.Services;
using WinBridge.Localization;

var tests = new (string Name, Func<Task> Run)[]
{
    ("設定Versionを段階的に移行できる", TestMigrationAsync),
    ("デバイスページを空にした設定を維持する", TestEmptyDevicePageMigrationAsync),
    ("新しいVersionを誤って上書きしない", TestFutureVersionAsync),
    ("同時保存でも有効なJSONを維持する", TestConcurrentSaveAsync),
    ("破損時に前回バックアップから復旧する", TestBackupRecoveryAsync),
    ("設定カタログが安全なURIだけを持つ", TestCatalogSafetyAsync),
    ("端末能力の実判定が例外なく完了する", TestRuntimeAvailabilityProbeAsync),
    ("利用できない機器依存設定を隠して保存を維持する", TestConditionalSettingAvailabilityAsync),
    ("デバイスページの設定を追加・解除できる", TestDevicePageSettingsAsync),
    ("全機能を非表示にしてデバイスと接続を再表示できる", TestModuleVisibilityAsync),
    ("お好み電源プリセットを保存して再利用できる", TestCustomPowerPresetAsync),
    ("画面起動先が確認済みURIだけを持つ", TestApprovedLaunchTargetsAsync),
    ("Windows画面の起動引数を分離できる", TestLauncherArgumentsAsync),
    ("設定URI以外をランチャーで拒否する", TestLauncherTargetValidationAsync),
    ("Windowsコマンドをタイムアウトできる", TestCommandTimeoutAsync),
    ("入れ子リストのマウスホイールをページへ転送する", TestNestedScrollRoutingAsync),
    ("画面文言に英語リソースの漏れがない", TestXamlTranslationsAsync),
    ("2個目の起動から既存起動へ通知できる", TestSingleInstanceAsync),
    ("表示言語をWindows言語から判定できる", TestLanguageSelectionAsync)
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
    Assert(result.Settings.DevicePageSettings.Count == 6,
        "デバイスページの初期6項目が移行されません。");
    Assert(result.Settings.DevicePageSettings.SequenceEqual(
        [
            "devices.mouse",
            "devices.typing",
            "devices.microphone",
            "devices.printers",
            "devices.camera",
            "devices.bluetooth"
        ], StringComparer.OrdinalIgnoreCase),
        "デバイスページの初期項目または順番が正しくありません。");
    Assert(result.Settings.CustomPowerPreset.AcDisplayMinutes == 10 &&
           result.Settings.CustomPowerPreset.AcSleepMinutes == 30,
        "お好み電源プリセットの初期値が正しくありません。");
    return Task.CompletedTask;
}

static Task TestEmptyDevicePageMigrationAsync()
{
    var settings = new AppSettings
    {
        Version = 4,
        DevicePageSettings = []
    };
    var result = AppSettingsMigrator.Migrate(settings);
    Assert(result.Settings.DevicePageSettings.Count == 0,
        "ユーザーが全て外したデバイスページ設定が復元されています。");
    var customized = AppSettingsMigrator.Migrate(new AppSettings
    {
        Version = 4,
        DevicePageSettings = ["devices.usb"]
    });
    Assert(customized.Settings.DevicePageSettings.SequenceEqual(["devices.usb"]),
        "ユーザーが編集したデバイスページ設定が上書きされています。");
    return Task.CompletedTask;
}

static Task TestFutureVersionAsync()
{
    var result = AppSettingsMigrator.Migrate(new AppSettings { Version = 999 });
    Assert(!result.CanSave, "未来Versionを保存可能として扱っています。");
    Assert(result.Settings.Version == 999, "未来Versionを書き換えています。");
    return Task.CompletedTask;
}

static Task TestLanguageSelectionAsync()
{
    Assert(LocalizationService.ResolveLanguage("system", "ja-JP") == "ja-JP",
        "日本語のWindowsで日本語が選ばれません。");
    Assert(LocalizationService.ResolveLanguage("system", "fr-FR") == "en-US",
        "日本語以外のWindowsで英語が選ばれません。");
    Assert(LocalizationService.ResolveLanguage("ja-JP", "en-US") == "ja-JP",
        "日本語固定の設定が優先されません。");
    Assert(LocalizationService.ResolveLanguage("en-US", "ja-JP") == "en-US",
        "英語固定の設定が優先されません。");

    var automatic = AppSettingsMigrator.Migrate(new AppSettings { Version = 5 }).Settings;
    Assert(automatic.Language == "system", "旧設定の言語がWindows自動判定になりません。");
    LocalizationService.Initialize("en-US");
    Assert(L.T("ホーム") == "Home", "英語リソースを読み込めません。");
    var definition = new SettingDefinition
    {
        Id = "devices.mouse",
        DisplayName = "マウス",
        Description = "マウスを設定します",
        Category = "デバイス",
        Target = "ms-settings:mouse"
    };
    CatalogLocalizationService.Localize(definition);
    Assert(definition.DisplayName == "Mouse" && definition.Category == "Devices",
        "設定カタログを英語化できません。");
    LocalizationService.Initialize("ja-JP");
    Assert(L.T("ホーム") == "ホーム", "日本語へ戻せません。");
    return Task.CompletedTask;
}

static async Task TestNestedScrollRoutingAsync()
{
    foreach (var viewName in new[] { "SettingsCatalogView", "ModuleSettingsView" })
    {
        var xaml = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Views", $"{viewName}.xaml"));
        Assert(xaml.Contains("x:Name=\"PageScrollViewer\"", StringComparison.Ordinal),
            $"{viewName}のページスクロール領域に名前がありません。");
        Assert(xaml.Contains("PreviewMouseWheel=\"NestedList_PreviewMouseWheel\"", StringComparison.Ordinal),
            $"{viewName}の入れ子リストがマウスホイールを転送しません。");
    }
}

static async Task TestXamlTranslationsAsync()
{
    var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(
        await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Resources", "Strings.en-US.json"))) ?? [];
    var missing = new HashSet<string>();
    var unwrapped = new List<string>();
    foreach (var file in Directory.EnumerateFiles(
                 Path.Combine(AppContext.BaseDirectory, "Views"), "*.xaml"))
    {
        var text = await File.ReadAllTextAsync(file);
        foreach (Match match in Regex.Matches(text, @"\{loc:Loc '([^']+)'\}"))
        {
            if (!translations.ContainsKey(match.Groups[1].Value))
                missing.Add(match.Groups[1].Value);
        }
        if (Regex.IsMatch(text,
                @"(?:Text|Content|ToolTip|AutomationProperties\.Name)=""(?!\{loc:Loc)([^""]*[\u3040-\u30ff\u3400-\u9fff])"))
            unwrapped.Add(Path.GetFileName(file));
    }
    Assert(missing.Count == 0, $"英訳のない画面文言があります: {string.Join(", ", missing)}");
    Assert(unwrapped.Count == 0, $"ローカライズされていない画面文言があります: {string.Join(", ", unwrapped)}");
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
        new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        }) ?? [];
    Assert(settings.Count >= 160, "設定カタログの項目数が想定より少なくなっています。");
    Assert(settings.Select(s => s.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == settings.Count,
        "設定カタログに重複IDがあります。");
    Assert(settings.All(s => Uri.TryCreate(s.Target, UriKind.Absolute, out var uri)
                             && uri.Scheme.Equals("ms-settings", StringComparison.OrdinalIgnoreCase)),
        "安全でない設定URIが含まれています。");
}

static async Task TestConditionalSettingAvailabilityAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var logger = new LoggingService(Path.Combine(directory, "logs"));
        var settingsService = new AppSettingsService(logger, directory);
        var settings = new AppSettings
        {
            Settings =
            [
                new SettingPreference
                    { Id = "system.display", Order = 1, IsFavorite = false, IsPinned = true },
                new SettingPreference
                    { Id = "system.battery-saver", Order = 2, IsFavorite = true, IsPinned = true }
            ]
        };
        var catalog = new SettingCatalogService(
            settingsService, settings, logger, new TestAvailabilityService(false));
        await catalog.LoadAsync();

        var conditional = catalog.AllSettings.Single(s => s.Id == "system.battery-saver");
        Assert(!conditional.IsAvailable, "利用できない機器依存設定が利用可能になっています。");
        Assert(catalog.SelectedSettings.All(s => s.Id != "system.battery-saver"),
            "利用できない機器依存設定が表示対象になっています。");
        Assert(settings.Settings.Any(s => s.Id == "system.battery-saver"),
            "非表示にした機器依存設定の保存データが失われました。");
        Assert(!(await catalog.AddAsync(conditional)).IsSuccess,
            "利用できない機器依存設定を追加できてしまいます。");
    }
    finally
    {
        DeleteTemporaryDirectory(directory);
    }
}

static Task TestRuntimeAvailabilityProbeAsync()
{
    var service = new SettingAvailabilityService();
    Assert(service.IsAvailable(SettingAvailability.Always),
        "常時利用可能な設定が利用不可になっています。");
    foreach (var availability in Enum.GetValues<SettingAvailability>())
        _ = service.IsAvailable(availability);
    return Task.CompletedTask;
}

static async Task TestDevicePageSettingsAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var logger = new LoggingService(Path.Combine(directory, "logs"));
        var settingsService = new AppSettingsService(logger, directory);
        var settings = AppSettingsMigrator.Migrate(new AppSettings { Version = 3 }).Settings;
        var catalog = new SettingCatalogService(
            settingsService, settings, logger, new TestAvailabilityService(false));
        await catalog.LoadAsync();
        var pageSettings = new DevicePageSettingsService(settingsService, settings, catalog, logger);

        Assert(pageSettings.SelectedSettings.Count == 6,
            "デバイスページの初期6項目が表示されません。");
        var bluetooth = pageSettings.SelectedSettings.Single(s => s.Id == "devices.bluetooth");
        Assert((await pageSettings.RemoveAsync(bluetooth)).IsSuccess,
            "デバイスページから設定を外せません。");
        Assert(!settings.DevicePageSettings.Contains("devices.bluetooth", StringComparer.OrdinalIgnoreCase),
            "外した設定が保存対象に残っています。");

        Assert(pageSettings.AvailableSettings.All(s => s.Category == "デバイス"),
            "デバイス以外の設定が追加候補に表示されています。");

        var display = catalog.AllSettings.Single(s => s.Id == "system.display");
        Assert(!(await pageSettings.AddAsync(display)).IsSuccess,
            "デバイス以外の設定をデバイスページへ追加できてしまいます。");

        var autoplay = catalog.AllSettings.Single(s => s.Id == "devices.autoplay");
        Assert((await pageSettings.AddAsync(autoplay)).IsSuccess,
            "デバイスカテゴリの設定をデバイスページへ追加できません。");
        Assert(settings.DevicePageSettings.Contains("devices.autoplay", StringComparer.OrdinalIgnoreCase),
            "追加した設定が保存対象にありません。");

        var unavailable = catalog.AllSettings.Single(s => s.Id == "devices.pen");
        Assert(!(await pageSettings.AddAsync(unavailable)).IsSuccess,
            "利用できない機器依存設定をデバイスページへ追加できてしまいます。");

        var reloaded = await new AppSettingsService(logger, directory).LoadAsync();
        Assert(reloaded.DevicePageSettings.Contains("devices.autoplay", StringComparer.OrdinalIgnoreCase) &&
               !reloaded.DevicePageSettings.Contains("devices.bluetooth", StringComparer.OrdinalIgnoreCase),
            "デバイスページの変更を再読み込みできません。");
    }
    finally
    {
        DeleteTemporaryDirectory(directory);
    }
}

static async Task TestModuleVisibilityAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var logger = new LoggingService(Path.Combine(directory, "logs"));
        var settingsService = new AppSettingsService(logger, directory);
        var settings = new AppSettings();
        var modules = new ModuleService(settingsService, settings, logger);
        await modules.LoadDefinitionsAsync();

        foreach (var module in modules.Modules)
            module.IsVisible = false;
        Assert((await modules.SaveAsync()).IsSuccess,
            "全機能を非表示にした設定を保存できません。");

        var hiddenSettings = await new AppSettingsService(logger, directory).LoadAsync();
        var hiddenModules = new ModuleService(settingsService, hiddenSettings, logger);
        await hiddenModules.LoadDefinitionsAsync();
        Assert(hiddenModules.Modules.Count > 0 && hiddenModules.Modules.All(module => !module.IsVisible),
            "全機能を非表示にした状態が維持されません。");
        Assert(!hiddenModules.IsVisible("devices"),
            "デバイスと接続が強制的に再表示されています。");

        hiddenModules.Modules.Single(module => module.Id == "devices").IsVisible = true;
        Assert((await hiddenModules.SaveAsync()).IsSuccess,
            "デバイスと接続の再表示設定を保存できません。");

        var restoredSettings = await new AppSettingsService(logger, directory).LoadAsync();
        var restoredModules = new ModuleService(settingsService, restoredSettings, logger);
        await restoredModules.LoadDefinitionsAsync();
        Assert(restoredModules.IsVisible("devices"),
            "デバイスと接続を再表示できません。");
        Assert(restoredModules.Modules.Where(module => module.Id != "devices")
            .All(module => !module.IsVisible),
            "再表示していない機能まで表示されています。");
    }
    finally
    {
        DeleteTemporaryDirectory(directory);
    }
}

static async Task TestCustomPowerPresetAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var logger = new LoggingService(Path.Combine(directory, "logs"));
        var settingsService = new AppSettingsService(logger, directory);
        var settings = AppSettingsMigrator.Migrate(new AppSettings { Version = 6 }).Settings;
        var service = new PowerPresetService(settingsService, settings);
        var preset = new PowerPresetSettings
        {
            AcDisplayMinutes = 5,
            AcSleepMinutes = 15,
            DcDisplayMinutes = 3,
            DcSleepMinutes = 10
        };

        Assert((await service.SaveAsync(preset)).IsSuccess,
            "お好み電源プリセットを保存できません。");
        var reloaded = await new AppSettingsService(logger, directory).LoadAsync();
        Assert(reloaded.Version == AppSettingsMigrator.CurrentVersion,
            "お好み電源プリセットの設定Versionが移行されません。");
        Assert(reloaded.CustomPowerPreset.AcDisplayMinutes == 5 &&
               reloaded.CustomPowerPreset.AcSleepMinutes == 15 &&
               reloaded.CustomPowerPreset.DcDisplayMinutes == 3 &&
               reloaded.CustomPowerPreset.DcSleepMinutes == 10,
            "保存したお好み電源プリセットを再読み込みできません。");
    }
    finally
    {
        DeleteTemporaryDirectory(directory);
    }
}

static async Task TestApprovedLaunchTargetsAsync()
{
    var catalogText = await File.ReadAllTextAsync(
        Path.Combine(AppContext.BaseDirectory, "Resources", "SettingDefinitions.json"));
    var approved = Regex.Matches(catalogText, @"ms-settings:[A-Za-z0-9-]+")
        .Select(match => match.Value)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var files = Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Resources"), "*.json")
        .Concat(Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Views"), "*.xaml"));
    var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var file in files)
    {
        var text = await File.ReadAllTextAsync(file);
        foreach (Match match in Regex.Matches(text, @"ms-settings:[A-Za-z0-9-]+"))
            targets.Add(match.Value);
    }

    var unapproved = targets.Where(target => !approved.Contains(target)).ToArray();
    Assert(unapproved.Length == 0,
        $"未確認のWindows設定URIがあります: {string.Join(", ", unapproved)}");
}

static Task TestLauncherArgumentsAsync()
{
    var control = WindowsSettingsLauncher.CreateControlPanelStartInfo(
        "/name", "Microsoft.IndexingOptions");
    Assert(Path.GetFileName(control.FileName).Equals("control.exe", StringComparison.OrdinalIgnoreCase),
        "コントロールパネルの実行ファイルが正しくありません。");
    Assert(control.ArgumentList.SequenceEqual(["/name", "Microsoft.IndexingOptions"]),
        "コントロールパネルの引数が分離されていません。");

    var deviceManager = WindowsSettingsLauncher.CreateDeviceManagerStartInfo();
    Assert(Path.GetFileName(deviceManager.FileName).Equals("mmc.exe", StringComparison.OrdinalIgnoreCase),
        "デバイスマネージャーがMMC経由になっていません。");
    Assert(deviceManager.ArgumentList.Count == 1 &&
           Path.GetFileName(deviceManager.ArgumentList[0]).Equals("devmgmt.msc", StringComparison.OrdinalIgnoreCase),
        "デバイスマネージャーのコンソール指定が正しくありません。");

    var folderOptions = WindowsSettingsLauncher.CreateFolderOptionsStartInfo();
    Assert(Path.GetFileName(folderOptions.FileName).Equals("rundll32.exe", StringComparison.OrdinalIgnoreCase),
        "フォルダー オプションの実行ファイルが正しくありません。");
    Assert(folderOptions.ArgumentList.SequenceEqual(["shell32.dll,Options_RunDLL", "0"]),
        "フォルダー オプションの引数が正しくありません。");
    return Task.CompletedTask;
}

static Task TestLauncherTargetValidationAsync()
{
    var valid = WindowsSettingsLauncher.CreateSettingsStartInfo("ms-settings:display");
    Assert(valid.FileName == "ms-settings:display", "有効な設定URIが変換されています。");

    try
    {
        WindowsSettingsLauncher.CreateSettingsStartInfo("notepad.exe");
        throw new InvalidOperationException("設定URI以外が許可されています。");
    }
    catch (ArgumentException)
    {
        return Task.CompletedTask;
    }
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
    var instanceName = $"test-{Guid.NewGuid():N}";
    using var first = new SingleInstanceService(instanceName);
    Assert(first.IsFirstInstance, "最初のインスタンスが所有権を取得できません。");
    var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    first.ListenForActivation(() => activated.TrySetResult());
    using var second = new SingleInstanceService(instanceName);
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

sealed class TestAvailabilityService(bool conditionalAvailability) : ISettingAvailabilityService
{
    public bool IsAvailable(SettingAvailability availability) =>
        availability == SettingAvailability.Always || conditionalAvailability;
}
