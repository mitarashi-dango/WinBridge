using System.Text.Json;
using WinBridge.Models;

namespace WinBridge.Services;

public sealed class AppSettingsService
{
    private readonly LoggingService _logger;
    private readonly string _directory;
    private readonly string _path;
    private readonly string _backupPath;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private bool _canSave = true;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AppSettingsService(LoggingService logger, string? appDataDirectory = null)
    {
        _logger = logger;
        _directory = appDataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinBridge");
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "settings.json");
        _backupPath = Path.Combine(_directory, "settings.backup.json");
    }

    public async Task<AppSettings> LoadAsync()
    {
        CleanupStaleTemporaryFiles();
        if (!File.Exists(_path))
            return AppSettingsMigrator.Migrate(new AppSettings()).Settings;

        var primary = await TryLoadFileAsync(_path);
        if (primary is not null)
            return ApplyMigration(primary);

        _logger.Error("設定ファイルを読み込めませんでした。");
        BackupBrokenSettings();

        var backup = await TryLoadFileAsync(_backupPath);
        if (backup is not null)
        {
            _logger.Info("前回の正常な設定バックアップから復旧しました。");
            return ApplyMigration(backup);
        }

        _logger.Error("利用できる設定バックアップがないため初期設定へ戻しました。");
        return AppSettingsMigrator.Migrate(new AppSettings()).Settings;
    }

    public async Task<OperationResult> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        if (!_canSave)
            return OperationResult.Failure(
                "新しいバージョンの設定ファイルを保護するため、設定を保存しませんでした。");

        try
        {
            await _saveGate.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Failure("設定の保存を中止しました。");
        }
        string? temporary = null;
        try
        {
            var migration = AppSettingsMigrator.Migrate(settings);
            if (!migration.CanSave)
            {
                _canSave = false;
                return OperationResult.Failure(migration.Warning ?? "設定ファイルを保存できません。");
            }

            // UI側で次の変更が始まる前に、保存内容を不変のバイト列として確定する。
            var bytes = JsonSerializer.SerializeToUtf8Bytes(migration.Settings, JsonOptions);
            temporary = Path.Combine(_directory, $"settings.{Guid.NewGuid():N}.tmp");
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                             FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(true);
            }

            if (File.Exists(_path))
                File.Copy(_path, _backupPath, true);
            File.Move(temporary, _path, true);
            temporary = null;
            return OperationResult.Success("設定を保存しました。");
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Failure("設定の保存を中止しました。");
        }
        catch (Exception ex)
        {
            _logger.Error("設定を保存できませんでした。", ex);
            return OperationResult.Failure(
                "設定を保存できませんでした。前回の設定は保護されています。",
                $"{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            if (temporary is not null)
            {
                try { File.Delete(temporary); }
                catch { }
            }
            _saveGate.Release();
        }
    }

    private AppSettings ApplyMigration(AppSettings settings)
    {
        var migration = AppSettingsMigrator.Migrate(settings);
        _canSave = migration.CanSave;
        if (migration.Warning is not null)
            _logger.Error(migration.Warning);
        return migration.Settings;
    }

    private static async Task<AppSettings?> TryLoadFileAsync(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions);
        }
        catch { return null; }
    }

    private void BackupBrokenSettings()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var backup = Path.Combine(_directory,
                $"settings.broken-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
            File.Move(_path, backup);
        }
        catch (Exception ex) { _logger.Error("破損設定の退避に失敗しました。", ex); }
    }

    private void CleanupStaleTemporaryFiles()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_directory, "settings.*.tmp"))
            {
                if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddHours(-1))
                    File.Delete(file);
            }
        }
        catch (Exception ex) { _logger.Error("古い一時設定ファイルを整理できませんでした。", ex); }
    }
}
