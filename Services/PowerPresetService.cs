using WinBridge.Models;

namespace WinBridge.Services;

public sealed class PowerPresetService
{
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
        if (preset.AcDisplayMinutes < 0 || preset.AcSleepMinutes < 0 ||
            preset.DcDisplayMinutes < 0 || preset.DcSleepMinutes < 0)
            return OperationResult.Failure("お好みプリセットの時間が正しくありません。");

        _settings.CustomPowerPreset = preset;
        return await _settingsService.SaveAsync(_settings);
    }
}
