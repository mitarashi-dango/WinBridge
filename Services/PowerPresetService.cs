using WinBridge.Models;

namespace WinBridge.Services;

public sealed class PowerPresetService
{
    public const int MaximumMinutes = 7 * 24 * 60;

    private readonly AppSettingsService _settingsService;
    private readonly AppSettings _settings;

    public PowerPresetService(AppSettingsService settingsService, AppSettings settings)
    {
        _settingsService = settingsService;
        _settings = settings;
    }

    public PowerPresetSettings Get() => _settings.CustomPowerPreset;

    public async Task<OperationResult> SaveAsync(PowerPresetSettings preset)
    {
        if (!IsValid(preset.AcDisplayMinutes) || !IsValid(preset.AcSleepMinutes) ||
            !IsValid(preset.DcDisplayMinutes) || !IsValid(preset.DcSleepMinutes))
            return OperationResult.Failure("お好みプリセットの時間が正しくありません。");

        _settings.CustomPowerPreset = preset;
        return await _settingsService.SaveAsync(_settings);
    }

    private static bool IsValid(int minutes) => minutes is >= 0 and <= MaximumMinutes;
}
