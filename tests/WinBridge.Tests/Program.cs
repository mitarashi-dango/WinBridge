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
    ("秒単位の電源設定を切り捨てず維持できる", TestPowerSecondsPreservedAsync),
    ("秒単位の現在値を画面で正確に維持できる", TestPowerViewSecondsAsync),
    ("電源設定の途中失敗時に変更前の値へ戻せる", TestPowerRollbackAsync),
    ("電源設定の反映不一致時に変更前の値へ戻せる", TestPowerReadbackMismatchAsync),
    ("操作中に電源プランが変わった場合は安全に中止できる", TestPowerSchemeSwitchAsync),
    ("異常に長い電源設定を拒否できる", TestPowerMaximumValidationAsync),
    ("画面起動先が確認済みURIだけを持つ", TestApprovedLaunchTargetsAsync),
    ("Windows画面の起動引数を分離できる", TestLauncherArgumentsAsync),
    ("設定URI以外をランチャーで拒否する", TestLauncherTargetValidationAsync),
    ("MSIX実行判定の戻り値を安全に分類できる", TestPackageIdentityClassificationAsync),
    ("GitHub配布版ではExplorer表示設定を維持する", TestGitHubExplorerControlsAsync),
    ("Store版ではExplorer設定の直接変更を拒否する", TestStoreExplorerRestrictionAsync),
    ("開発支援リンクを公式Ko-fiページだけに制限する", TestSupportLinkValidationAsync),
    ("Windows標準コマンドをSystem32から起動する", TestSystemExecutableResolutionAsync),
    ("Windowsコマンドをタイムアウトできる", TestCommandTimeoutAsync),
    ("画面外のウィンドウ位置を表示領域へ戻せる", TestWindowBoundsAsync),
    ("アプリのバージョンを画面用に整形できる", TestVersionDisplayAsync),
    ("配布設定がWindows 11・多言語・署名必須になっている", TestReleaseHardeningAsync),
    ("入れ子リストのマウスホイールをページへ転送する", TestNestedScrollRoutingAsync),
    ("画面文言の4翻訳リソースが揃っている", TestXamlTranslationsAsync),
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
        "未対応言語のWindowsで英語へフォールバックしません。");
    Assert(LocalizationService.ResolveLanguage("system", "es-MX") == "es-ES",
        "スペイン語のWindowsでスペイン語が選ばれません。");
    Assert(LocalizationService.ResolveLanguage("system", "zh-CN") == "zh-CN" &&
           LocalizationService.ResolveLanguage("system", "zh-Hans") == "zh-CN",
        "簡体字中国語のWindowsで簡体字が選ばれません。");
    Assert(LocalizationService.ResolveLanguage("system", "zh-TW") == "zh-TW" &&
           LocalizationService.ResolveLanguage("system", "zh-HK") == "zh-TW" &&
           LocalizationService.ResolveLanguage("system", "zh-Hant") == "zh-TW",
        "繁体字中国語のWindowsで繁体字が選ばれません。");
    Assert(LocalizationService.ResolveLanguage("ja-JP", "en-US") == "ja-JP",
        "日本語固定の設定が優先されません。");
    Assert(LocalizationService.ResolveLanguage("zh-Hans", "ja-JP") == "zh-CN" &&
           LocalizationService.ResolveLanguage("zh-Hant", "ja-JP") == "zh-TW",
        "中国語の言語コード別名を正規化できません。");

    var automatic = AppSettingsMigrator.Migrate(new AppSettings { Version = 5 }).Settings;
    Assert(automatic.Language == "system", "旧設定の言語がWindows自動判定になりません。");

    foreach (var (language, home, settingName, category) in new[]
             {
                 ("en-US", "Home", "Mouse", "Devices"),
                 ("es-ES", "Inicio", "Ratón", "Dispositivos"),
                 ("zh-CN", "主页", "鼠标", "设备"),
                 ("zh-TW", "首頁", "滑鼠", "裝置")
             })
    {
        LocalizationService.Initialize(language);
        Assert(L.T("ホーム") == home, $"{language} の翻訳リソースを読み込めません。");
        var definition = new SettingDefinition
        {
            Id = "devices.mouse",
            DisplayName = "マウス",
            Description = "マウスを設定します",
            Category = "デバイス",
            Target = "ms-settings:mouse"
        };
        CatalogLocalizationService.Localize(definition);
        Assert(definition.DisplayName == settingName && definition.Category == category,
            $"{language} の設定カタログをローカライズできません。");
    }

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
    foreach (var language in new[] { "es-ES", "zh-CN", "zh-TW" })
    {
        var localized = JsonSerializer.Deserialize<Dictionary<string, string>>(
            await File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, "Resources", $"Strings.{language}.json"))) ?? [];
        var missingKeys = translations.Keys.Except(localized.Keys).ToArray();
        var extraKeys = localized.Keys.Except(translations.Keys).ToArray();
        var blankValues = localized.Where(item => string.IsNullOrWhiteSpace(item.Value))
            .Select(item => item.Key).ToArray();
        var placeholderMismatches = translations.Keys.Where(key =>
                Placeholders(key) != Placeholders(localized.GetValueOrDefault(key, "")))
            .ToArray();
        Assert(missingKeys.Length == 0,
            $"{language} に不足する翻訳キーがあります: {string.Join(", ", missingKeys)}");
        Assert(extraKeys.Length == 0,
            $"{language} に英語辞書と一致しないキーがあります: {string.Join(", ", extraKeys)}");
        Assert(blankValues.Length == 0,
            $"{language} に空の翻訳があります: {string.Join(", ", blankValues)}");
        Assert(placeholderMismatches.Length == 0,
            $"{language} の書式プレースホルダーが一致しません: {string.Join(", ", placeholderMismatches)}");
    }

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

    foreach (var fileName in new[]
             {
                 "Services/PowerPolicyAccessor.cs",
                 "Services/PowerSettingsService.cs",
                 "Services/PowerPresetService.cs",
                 "ViewModels/PowerViewModel.cs"
             })
    {
        var file = Path.Combine(AppContext.BaseDirectory, "SourceFiles",
            fileName.Replace('/', Path.DirectorySeparatorChar));
        var text = await File.ReadAllTextAsync(file);
        foreach (Match match in Regex.Matches(text,
                     @"(?:OperationResult(?:<[^>]+>)?\.(?:Success|Failure)|L\.(?:T|F))\(\s*""((?:\\.|[^""])*)""",
                     RegexOptions.Singleline))
        {
            var source = Regex.Unescape(match.Groups[1].Value);
            if (source.Length > 0 && Regex.IsMatch(source, @"[\u3040-\u30ff\u3400-\u9fff]") &&
                !translations.ContainsKey(source))
                missing.Add(source);
        }
    }
    Assert(missing.Count == 0,
        $"英訳のない電源設定文言があります: {string.Join(", ", missing)}");

    static string Placeholders(string value) =>
        string.Join("|", Regex.Matches(value, @"\{[^}]+\}")
            .Select(match => match.Value)
            .OrderBy(value => value, StringComparer.Ordinal));
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

static async Task TestPowerRollbackAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var policy = CreatePowerPolicy(600, 1800, 300, 900);
        policy.FailOnWriteNumber = 2;
        var logger = new LoggingService(Path.Combine(directory, "logs"));
        var service = new PowerSettingsService(logger, policy, () => false);
        var result = await service.ApplyAsync(new PowerSettings(
            false, 900, 1800, 600, 1800));

        Assert(!result.IsSuccess, "途中で失敗した電源設定が成功扱いになっています。");
        Assert(result.UserMessage.Contains("変更前の値へ戻しました", StringComparison.Ordinal),
            "電源設定を元へ戻したことが通知されていません。");
        Assert(policy.Writes.Any(write =>
                write.Source == PowerSource.Ac &&
                write.SettingId == PowerSettingsService.DisplayTimeoutId &&
                write.Seconds == 900),
            "最初の電源設定変更が実行されていません。");
        Assert(policy.GetValue(PowerSettingsService.VideoSubgroupId,
                   PowerSettingsService.DisplayTimeoutId, PowerSource.Ac) == 600 &&
               policy.GetValue(PowerSettingsService.SleepSubgroupId,
                   PowerSettingsService.SleepTimeoutId, PowerSource.Ac) == 1800,
            "変更済みの秒単位設定が元の値へ戻されていません。");
    }
    finally
    {
        DeleteTemporaryDirectory(directory);
    }
}

static async Task TestPowerSecondsPreservedAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var policy = CreatePowerPolicy(30, 59, 45, 90);
        var logger = new LoggingService(Path.Combine(directory, "logs"));
        var service = new PowerSettingsService(logger, policy, () => true);

        var current = await service.GetAsync();
        Assert(current.IsSuccess && current.Value is not null,
            "秒単位の電源設定を読み取れません。");
        var settings = current.Value ?? throw new InvalidOperationException("電源設定がありません。");
        Assert(settings.AcDisplaySeconds == 30 && settings.AcSleepSeconds == 59 &&
               settings.DcDisplaySeconds == 45 && settings.DcSleepSeconds == 90,
            "1分未満の値が切り捨てられています。");

        var result = await service.ApplyAsync(settings);
        Assert(result.IsSuccess, "秒単位の現在値をそのまま適用できません。");
        Assert(policy.Writes.Take(4).Select(write => write.Seconds)
                .SequenceEqual([30u, 59u, 45u, 90u]),
            "秒単位の値が別の値へ変換されて書き込まれています。");
    }
    finally
    {
        DeleteTemporaryDirectory(directory);
    }
}

static async Task TestPowerViewSecondsAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        LocalizationService.Initialize("ja-JP");
        var policy = CreatePowerPolicy(30, 59, 45, 90);
        var logger = new LoggingService(Path.Combine(directory, "logs"));
        var power = new PowerSettingsService(logger, policy, () => true);
        var appSettings = new AppSettings();
        var settingsService = new AppSettingsService(logger, directory);
        var preset = new PowerPresetService(settingsService, appSettings);
        var reports = new List<OperationResult>();
        var viewModel = new WinBridge.ViewModels.PowerViewModel(power, preset, reports.Add);

        await viewModel.RefreshAsync();
        Assert(viewModel.AcDisplay?.Seconds == 30 &&
               viewModel.AcDisplay.Label.Contains("30秒", StringComparison.Ordinal),
            "画面上で30秒の現在値が「なし」へ変換されています。");

        policy.SetValue(PowerSettingsService.VideoSubgroupId,
            PowerSettingsService.DisplayTimeoutId, PowerSource.Ac, 31);
        await viewModel.RefreshAsync();
        Assert(viewModel.AcDisplay?.Seconds == 31 &&
               viewModel.DisplayChoices.All(choice => choice.Seconds != 30),
            "再読み込み後に古い秒単位の現在値が残っています。");
        Assert(!viewModel.IsBusy && viewModel.IsInteractionEnabled,
            "読み込み完了後も電源設定の操作が無効になっています。");
    }
    finally
    {
        DeleteTemporaryDirectory(directory);
    }
}

static async Task TestPowerReadbackMismatchAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var policy = CreatePowerPolicy(600, 1800, 300, 900);
        policy.ReturnMismatchedReadback = true;
        var logger = new LoggingService(Path.Combine(directory, "logs"));
        var service = new PowerSettingsService(logger, policy, () => false);

        var result = await service.ApplyAsync(new PowerSettings(
            false, 300, 600, 300, 900));

        Assert(!result.IsSuccess, "反映値が異なる電源設定が成功扱いになっています。");
        Assert(result.UserMessage.Contains("変更前の値へ戻しました", StringComparison.Ordinal),
            "反映不一致後の復元が通知されていません。");
        Assert(policy.GetValue(PowerSettingsService.VideoSubgroupId,
                   PowerSettingsService.DisplayTimeoutId, PowerSource.Ac) == 600 &&
               policy.GetValue(PowerSettingsService.SleepSubgroupId,
                   PowerSettingsService.SleepTimeoutId, PowerSource.Ac) == 1800,
            "反映不一致後に変更前の値へ戻っていません。");
    }
    finally
    {
        DeleteTemporaryDirectory(directory);
    }
}

static async Task TestPowerSchemeSwitchAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var policy = CreatePowerPolicy(600, 1800, 300, 900);
        var originalScheme = policy.ActiveScheme;
        policy.SwitchSchemeOnActiveReadNumber = 3;
        var logger = new LoggingService(Path.Combine(directory, "logs"));
        var service = new PowerSettingsService(logger, policy, () => false);

        var result = await service.ApplyAsync(new PowerSettings(
            false, 300, 600, 300, 900));

        Assert(!result.IsSuccess && result.UserMessage.Contains("電源プラン", StringComparison.Ordinal),
            "操作中の電源プラン切り替えが検出されていません。");
        Assert(policy.ActiveScheme != originalScheme,
            "利用者が切り替えた電源プランを元へ戻してしまっています。");
        Assert(policy.GetValue(PowerSettingsService.VideoSubgroupId,
                   PowerSettingsService.DisplayTimeoutId, PowerSource.Ac, originalScheme) == 600,
            "元の電源プランに途中の変更が残っています。");
    }
    finally
    {
        DeleteTemporaryDirectory(directory);
    }
}

static async Task TestPowerMaximumValidationAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var policy = CreatePowerPolicy(600, 1800, 300, 900);
        var logger = new LoggingService(Path.Combine(directory, "logs"));
        var service = new PowerSettingsService(logger, policy, () => false);

        var result = await service.ApplyAsync(new PowerSettings(
            false, PowerSettingsService.MaximumTimeoutSeconds + 1, 600, 300, 900));

        Assert(!result.IsSuccess, "異常に長い電源設定が許可されています。");
        Assert(policy.Writes.Count == 0 && policy.ActiveReadCount == 0,
            "不正な値の検証前にWindowsの電源設定へアクセスしています。");
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

static Task TestPackageIdentityClassificationAsync()
{
    Assert(!PackageIdentityService.IsPackagedResult(PackageIdentityService.AppModelErrorNoPackage),
        "パッケージ外実行がMSIXとして判定されています。");
    Assert(PackageIdentityService.IsPackagedResult(122),
        "MSIXで返るバッファー不足コードをパッケージ外として扱っています。");
    Assert(PackageIdentityService.IsPackagedResult(0),
        "正常なパッケージ情報取得をパッケージ外として扱っています。");
    return Task.CompletedTask;
}

static async Task TestGitHubExplorerControlsAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var service = new ExplorerSettingsService(new LoggingService(directory), true);
        Assert(service.CanChangeSettingsDirectly,
            "GitHub配布版でExplorer設定の直接変更が無効です。");

        var view = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Views", "ExplorerView.xaml"));
        Assert(view.Contains("Visibility=\"{Binding CanChangeSettingsDirectly", StringComparison.Ordinal),
            "GitHub配布版でExplorer表示設定カードが表示されません。");
        Assert(view.Contains("'ファイル名拡張子を表示する'", StringComparison.Ordinal) &&
               view.Contains("'隠しファイルを表示する'", StringComparison.Ordinal),
            "GitHub配布版のExplorer表示設定が画面から削除されています。");
    }
    finally
    {
        DeleteTemporaryDirectory(directory);
    }
}

static Task TestStoreExplorerRestrictionAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var service = new ExplorerSettingsService(new LoggingService(directory), false);
        Assert(!service.CanChangeSettingsDirectly,
            "Store版でExplorer設定の直接変更が有効です。");
        Assert(!service.Get().IsSuccess,
            "Store版でExplorerレジストリの読み取りを実行できます。");
        Assert(!service.Apply(true, true).IsSuccess,
            "Store版でExplorerレジストリの書き込みを実行できます。");
        Assert(!service.Undo().IsSuccess,
            "Store版でExplorerレジストリの復元を実行できます。");
    }
    finally
    {
        DeleteTemporaryDirectory(directory);
    }

    return Task.CompletedTask;
}

static Task TestSupportLinkValidationAsync()
{
    var valid = ExternalLinkService.CreateSupportPageStartInfo("https://ko-fi.com/nioudachi");
    Assert(valid.FileName == "https://ko-fi.com/nioudachi",
        "公式Ko-fiページのURLが変換されています。");
    Assert(valid.UseShellExecute, "支援ページが既定のブラウザーで開かれません。");

    foreach (var invalid in new[]
             {
                 "http://ko-fi.com/nioudachi",
                 "https://example.com/nioudachi",
                 "https://ko-fi.com/another-account"
             })
    {
        try
        {
            ExternalLinkService.CreateSupportPageStartInfo(invalid);
            throw new InvalidOperationException($"許可されていない支援URLです: {invalid}");
        }
        catch (ArgumentException)
        {
        }
    }

    return Task.CompletedTask;
}

static Task TestSystemExecutableResolutionAsync()
{
    var resolved = CommandRunner.ResolveSystemExecutable("powercfg.exe");
    Assert(string.Equals(Path.GetDirectoryName(resolved),
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            StringComparison.OrdinalIgnoreCase),
        "Windows標準コマンドがSystem32へ固定されていません。");

    try
    {
        CommandRunner.ResolveSystemExecutable(@"..\powercfg.exe");
        throw new InvalidOperationException("相対パスを含む実行ファイル名が許可されています。");
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

static Task TestWindowBoundsAsync()
{
    var visible = new System.Windows.Rect(0, 0, 1920, 1080);
    var restored = WinBridge.App.ClampToVisibleArea(
        new System.Windows.Rect(5000, -2000, 1100, 720), visible);
    Assert(restored.Left == 820 && restored.Top == 0,
        "画面外のウィンドウ位置が表示領域へ戻されていません。");

    var secondaryMonitor = new System.Windows.Rect(-1920, 0, 3840, 1080);
    var preserved = WinBridge.App.ClampToVisibleArea(
        new System.Windows.Rect(-1500, 100, 1100, 720), secondaryMonitor);
    Assert(preserved.Left == -1500 && preserved.Top == 100,
        "表示領域内のマルチモニター座標が変更されています。");
    return Task.CompletedTask;
}

static Task TestVersionDisplayAsync()
{
    Assert(WinBridge.ViewModels.MainViewModel.FormatVersion(new Version(1, 1, 1, 0)) == "v1.1.1",
        "画面のバージョン表記がv1.1.1形式ではありません。");
    Assert(WinBridge.ViewModels.MainViewModel.FormatVersion(null) == "",
        "バージョンを取得できない場合の表示が安全ではありません。");
    return Task.CompletedTask;
}

static async Task TestReleaseHardeningAsync()
{
    var releaseFiles = Path.Combine(AppContext.BaseDirectory, "ReleaseFiles");
    var installer = await File.ReadAllTextAsync(Path.Combine(releaseFiles, "WinBridge.iss"));
    Assert(installer.Contains("MinVersion=10.0.22000", StringComparison.Ordinal),
        "インストーラーの最低OSがWindows 11になっていません。");
    Assert(installer.Contains("Name: \"english\"", StringComparison.Ordinal) &&
           installer.Contains("Name: \"japanese\"", StringComparison.Ordinal) &&
           installer.Contains("Name: \"spanish\"", StringComparison.Ordinal) &&
           installer.Contains("Name: \"chinesesimplified\"", StringComparison.Ordinal) &&
           installer.Contains("Name: \"chinesetraditional\"", StringComparison.Ordinal),
        "インストーラーに対応する5言語が登録されていません。");
    Assert(installer.Contains("#define AppVersion \"1.1.5\"", StringComparison.Ordinal),
        "インストーラーの既定バージョンが1.1.5ではありません。");

    var manifest = await File.ReadAllTextAsync(Path.Combine(releaseFiles, "app.manifest"));
    Assert(manifest.Contains("assemblyIdentity version=\"1.1.5.0\"", StringComparison.Ordinal),
        "アプリマニフェストのバージョンが1.1.5.0ではありません。");

    var msixManifest = await File.ReadAllTextAsync(
        Path.Combine(releaseFiles, "AppxManifest.template.xml"));
    Assert(msixManifest.Contains("Name=\"runFullTrust\"", StringComparison.Ordinal),
        "パッケージ化されたWPFアプリに必要なrunFullTrustがありません。");
    Assert(new[] { "ja-jp", "en-us", "es-es", "zh-cn", "zh-tw" }.All(language =>
            msixManifest.Contains($"Resource Language=\"{language}\"", StringComparison.Ordinal)),
        "MSIXマニフェストに対応する5言語が登録されていません。");
    Assert(!msixManifest.Contains("unvirtualizedResources", StringComparison.Ordinal) &&
           !msixManifest.Contains("RegistryWriteVirtualization", StringComparison.Ordinal),
        "Storeで承認されていないレジストリ仮想化解除がMSIXマニフェストに残っています。");

    var packageScript = await File.ReadAllTextAsync(
        Path.Combine(releaseFiles, "package-release.ps1"));
    Assert(packageScript.Contains("[string]$Version = \"1.1.5\"", StringComparison.Ordinal),
        "配布スクリプトの既定バージョンが1.1.5ではありません。");
    Assert(packageScript.Contains("SigningCertificateThumbprint", StringComparison.Ordinal) &&
           packageScript.Contains("AllowUnsigned", StringComparison.Ordinal) &&
           packageScript.Contains("A trusted code-signing certificate is required",
               StringComparison.Ordinal),
        "正式な配布物でコード署名を必須にする処理がありません。");
    Assert(typeof(WinBridge.App).Assembly.GetName().Version == new Version(1, 1, 5, 0),
        "アプリ本体のアセンブリバージョンが1.1.5.0ではありません。");
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

static FakePowerPolicyAccessor CreatePowerPolicy(
    uint acDisplay, uint acSleep, uint dcDisplay, uint dcSleep)
{
    var policy = new FakePowerPolicyAccessor();
    policy.SetValue(PowerSettingsService.VideoSubgroupId,
        PowerSettingsService.DisplayTimeoutId, PowerSource.Ac, acDisplay);
    policy.SetValue(PowerSettingsService.SleepSubgroupId,
        PowerSettingsService.SleepTimeoutId, PowerSource.Ac, acSleep);
    policy.SetValue(PowerSettingsService.VideoSubgroupId,
        PowerSettingsService.DisplayTimeoutId, PowerSource.Dc, dcDisplay);
    policy.SetValue(PowerSettingsService.SleepSubgroupId,
        PowerSettingsService.SleepTimeoutId, PowerSource.Dc, dcSleep);
    return policy;
}

sealed class TestAvailabilityService(bool conditionalAvailability) : ISettingAvailabilityService
{
    public bool IsAvailable(SettingAvailability availability) =>
        availability == SettingAvailability.Always || conditionalAvailability;
}

sealed class FakePowerPolicyAccessor : IPowerPolicyAccessor
{
    private readonly Dictionary<(Guid SchemeId, Guid SubgroupId, Guid SettingId, PowerSource Source), uint>
        _values = [];

    public Guid ActiveScheme { get; private set; } = Guid.NewGuid();
    public int ActiveReadCount { get; private set; }
    public int? SwitchSchemeOnActiveReadNumber { get; set; }
    public int? FailOnWriteNumber { get; set; }
    public bool ReturnMismatchedReadback { get; set; }
    public int ActivationCount { get; private set; }
    public List<PowerWrite> Writes { get; } = [];

    public OperationResult<Guid> GetActiveScheme()
    {
        ActiveReadCount++;
        if (SwitchSchemeOnActiveReadNumber == ActiveReadCount)
            ActiveScheme = Guid.NewGuid();
        return OperationResult<Guid>.Success(ActiveScheme);
    }

    public OperationResult<uint> ReadValue(
        Guid schemeId, Guid subgroupId, Guid settingId, PowerSource source)
    {
        if (!_values.TryGetValue((schemeId, subgroupId, settingId, source), out var value))
            return OperationResult<uint>.Failure("テスト値がありません。");
        if (ReturnMismatchedReadback && ActivationCount == 1 &&
            settingId == PowerSettingsService.DisplayTimeoutId && source == PowerSource.Ac)
            value++;
        return OperationResult<uint>.Success(value);
    }

    public OperationResult WriteValue(
        Guid schemeId, Guid subgroupId, Guid settingId, PowerSource source, uint seconds)
    {
        Writes.Add(new PowerWrite(schemeId, subgroupId, settingId, source, seconds));
        if (FailOnWriteNumber == Writes.Count)
            return OperationResult.Failure("テスト用の変更失敗");
        _values[(schemeId, subgroupId, settingId, source)] = seconds;
        return OperationResult.Success("");
    }

    public OperationResult ActivateScheme(Guid schemeId)
    {
        ActiveScheme = schemeId;
        ActivationCount++;
        return OperationResult.Success("");
    }

    public void SetValue(Guid subgroupId, Guid settingId, PowerSource source, uint seconds) =>
        _values[(ActiveScheme, subgroupId, settingId, source)] = seconds;

    public uint GetValue(
        Guid subgroupId, Guid settingId, PowerSource source, Guid? schemeId = null) =>
        _values[(schemeId ?? ActiveScheme, subgroupId, settingId, source)];
}

sealed record PowerWrite(
    Guid SchemeId, Guid SubgroupId, Guid SettingId, PowerSource Source, uint Seconds);
