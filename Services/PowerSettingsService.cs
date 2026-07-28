using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using WinBridge.Models;

namespace WinBridge.Services;

public sealed partial class PowerSettingsService
{
    private readonly LoggingService _logger;
    public PowerSettingsService(LoggingService logger) => _logger = logger;

    public async Task<OperationResult<PowerSettings>> GetAsync()
    {
        var display = await QueryAsync("SUB_VIDEO", "VIDEOIDLE");
        var sleep = await QueryAsync("SUB_SLEEP", "STANDBYIDLE");
        if (!display.IsSuccess || !sleep.IsSuccess)
            return OperationResult<PowerSettings>.Failure(
                "電源設定を取得できませんでした。Windowsの設定画面で確認してください。",
                display.TechnicalDetails ?? sleep.TechnicalDetails);

        var hasBattery = HasBattery();
        _logger.Info("電源設定を取得しました。");
        return OperationResult<PowerSettings>.Success(new PowerSettings(hasBattery,
            display.Value.Ac, sleep.Value.Ac,
            display.Value.Dc, sleep.Value.Dc));
    }

    public async Task<OperationResult> ApplyAsync(PowerSettings value)
    {
        var commands = new List<string[]>
        {
            new[] { "-change", "-monitor-timeout-ac", value.AcDisplayMinutes.ToString() },
            new[] { "-change", "-standby-timeout-ac", value.AcSleepMinutes.ToString() }
        };
        if (value.HasBattery)
        {
            commands.Add(new[] { "-change", "-monitor-timeout-dc", value.DcDisplayMinutes.ToString() });
            commands.Add(new[] { "-change", "-standby-timeout-dc", value.DcSleepMinutes.ToString() });
        }

        foreach (var args in commands)
        {
            var result = await CommandRunner.RunAsync("powercfg.exe", args);
            if (!result.IsSuccess)
            {
                _logger.Error("電源設定の変更に失敗しました。");
                return OperationResult.Failure("電源設定を変更できませんでした。", result.TechnicalDetails);
            }
        }
        _logger.Info("電源設定を変更しました。");
        return OperationResult.Success("電源設定を適用しました。");
    }

    private static async Task<OperationResult<(int Ac, int Dc)>> QueryAsync(string subgroup, string setting)
    {
        var result = await CommandRunner.RunAsync("powercfg.exe", "-query", "SCHEME_CURRENT", subgroup, setting);
        if (!result.IsSuccess) return OperationResult<(int, int)>.Failure(result.UserMessage, result.TechnicalDetails);
        var matches = HexValueRegex().Matches(result.Value ?? "");
        if (matches.Count < 2)
            return OperationResult<(int, int)>.Failure("電源設定の値を読み取れませんでした。");
        var acSeconds = Convert.ToInt64(matches[^2].Value[2..], 16);
        var dcSeconds = Convert.ToInt64(matches[^1].Value[2..], 16);
        return OperationResult<(int, int)>.Success(((int)(acSeconds / 60), (int)(dcSeconds / 60)));
    }

    private static bool HasBattery()
    {
        return GetSystemPowerStatus(out var status)
               && status.BatteryFlag != 255
               && (status.BatteryFlag & 128) == 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus systemPowerStatus);

    [GeneratedRegex(@"0x[0-9a-fA-F]{8}")]
    private static partial Regex HexValueRegex();
}
