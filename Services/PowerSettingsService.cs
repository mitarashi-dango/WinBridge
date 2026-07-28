using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using WinBridge.Models;

namespace WinBridge.Services;

public sealed partial class PowerSettingsService
{
    private readonly LoggingService _logger;
    private readonly Func<string, string[], Task<OperationResult<string>>> _run;

    public PowerSettingsService(LoggingService logger)
        : this(logger, (fileName, arguments) => CommandRunner.RunAsync(fileName, arguments)) { }

    internal PowerSettingsService(
        LoggingService logger,
        Func<string, string[], Task<OperationResult<string>>> run)
    {
        _logger = logger;
        _run = run;
    }

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
        if (value.AcDisplayMinutes < 0 || value.AcSleepMinutes < 0 ||
            value.DcDisplayMinutes < 0 || value.DcSleepMinutes < 0)
            return OperationResult.Failure("電源設定の時間が正しくありません。");

        var original = await GetAsync();
        if (!original.IsSuccess || original.Value is null)
            return OperationResult.Failure(
                "変更前の電源設定を確認できなかったため、設定を変更しませんでした。",
                original.TechnicalDetails);

        var commands = BuildCommands(value, value.HasBattery);
        for (var index = 0; index < commands.Count; index++)
        {
            var result = await _run("powercfg.exe", commands[index]);
            if (!result.IsSuccess)
            {
                _logger.Error("電源設定の変更に失敗しました。");
                var rollback = await RollbackAsync(original.Value, value.HasBattery, index);
                if (rollback.IsSuccess)
                {
                    _logger.Info("途中まで変更された電源設定を元の値へ戻しました。");
                    return OperationResult.Failure(
                        "電源設定を変更できなかったため、変更前の値へ戻しました。",
                        result.TechnicalDetails);
                }

                _logger.Error("途中まで変更された電源設定を完全には元へ戻せませんでした。");
                return OperationResult.Failure(
                    "電源設定の変更に失敗し、元の値へ完全には戻せませんでした。Windowsの設定画面で確認してください。",
                    $"{result.TechnicalDetails}; 復元: {rollback.TechnicalDetails}");
            }
        }
        _logger.Info("電源設定を変更しました。");
        return OperationResult.Success("電源設定を適用しました。");
    }

    private async Task<OperationResult> RollbackAsync(
        PowerSettings original, bool includeBattery, int appliedCommandCount)
    {
        var restoreCommands = BuildCommands(original, includeBattery);
        for (var index = appliedCommandCount - 1; index >= 0; index--)
        {
            var result = await _run("powercfg.exe", restoreCommands[index]);
            if (!result.IsSuccess)
                return OperationResult.Failure("電源設定を元へ戻せませんでした。", result.TechnicalDetails);
        }
        return OperationResult.Success("電源設定を元へ戻しました。");
    }

    private static List<string[]> BuildCommands(PowerSettings value, bool includeBattery)
    {
        var commands = new List<string[]>
        {
            new[] { "-change", "-monitor-timeout-ac", value.AcDisplayMinutes.ToString() },
            new[] { "-change", "-standby-timeout-ac", value.AcSleepMinutes.ToString() }
        };
        if (includeBattery)
        {
            commands.Add(new[] { "-change", "-monitor-timeout-dc", value.DcDisplayMinutes.ToString() });
            commands.Add(new[] { "-change", "-standby-timeout-dc", value.DcSleepMinutes.ToString() });
        }
        return commands;
    }

    private async Task<OperationResult<(int Ac, int Dc)>> QueryAsync(string subgroup, string setting)
    {
        var result = await _run("powercfg.exe", ["-query", "SCHEME_CURRENT", subgroup, setting]);
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
