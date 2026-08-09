using System.Runtime.InteropServices;
using WinBridge.Models;

namespace WinBridge.Services;

public sealed class PowerSettingsService
{
    public const uint MaximumTimeoutSeconds = 30u * 24u * 60u * 60u;

    internal static readonly Guid VideoSubgroupId = new("7516b95f-f776-4464-8c53-06167f40cc99");
    internal static readonly Guid DisplayTimeoutId = new("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");
    internal static readonly Guid SleepSubgroupId = new("238c9fa8-0aad-41ed-83f4-97be242c8f20");
    internal static readonly Guid SleepTimeoutId = new("29f6c1db-86da-48c5-9fdb-f2b67b1f44da");

    private readonly LoggingService _logger;
    private readonly IPowerPolicyAccessor _policy;
    private readonly Func<bool> _hasBattery;
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    public PowerSettingsService(LoggingService logger)
        : this(logger, new PowerPolicyAccessor(), HasBattery) { }

    internal PowerSettingsService(
        LoggingService logger, IPowerPolicyAccessor policy, Func<bool>? hasBattery = null)
    {
        _logger = logger;
        _policy = policy;
        _hasBattery = hasBattery ?? (() => false);
    }

    public async Task<OperationResult<PowerSettings>> GetAsync()
    {
        await _operationGate.WaitAsync();
        try
        {
            var snapshot = GetSnapshot();
            if (!snapshot.IsSuccess || snapshot.Value is null)
                return OperationResult<PowerSettings>.Failure(
                    snapshot.UserMessage, snapshot.TechnicalDetails);

            _logger.Info("電源設定を取得しました。");
            return OperationResult<PowerSettings>.Success(snapshot.Value.Settings);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<OperationResult> ApplyAsync(PowerSettings value)
    {
        if (!IsValid(value))
            return OperationResult.Failure("電源設定の時間が正しくありません。");

        await _operationGate.WaitAsync();
        try
        {
            var original = GetSnapshot();
            if (!original.IsSuccess || original.Value is null)
                return OperationResult.Failure(
                    "変更前の電源設定を確認できなかったため、設定を変更しませんでした。",
                    original.TechnicalDetails);

            var snapshot = original.Value;
            var includeBattery = value.HasBattery && snapshot.Settings.HasBattery;
            var changes = BuildChanges(value, includeBattery);

            for (var index = 0; index < changes.Count; index++)
            {
                var activeCheck = IsStillActive(snapshot.SchemeId);
                if (!activeCheck.IsSuccess)
                {
                    if (index == 0)
                        return OperationResult.Failure(activeCheck.UserMessage, activeCheck.TechnicalDetails);
                    return RestoreAfterFailure(snapshot, includeBattery,
                        activeCheck.UserMessage, activeCheck.TechnicalDetails);
                }

                var change = changes[index];
                var write = _policy.WriteValue(
                    snapshot.SchemeId, change.SubgroupId, change.SettingId,
                    change.Source, change.Seconds);
                if (!write.IsSuccess)
                    return RestoreAfterFailure(snapshot, includeBattery,
                        "電源設定を変更できませんでした。", write.TechnicalDetails);
            }

            var beforeActivation = IsStillActive(snapshot.SchemeId);
            if (!beforeActivation.IsSuccess)
                return RestoreAfterFailure(snapshot, includeBattery,
                    beforeActivation.UserMessage, beforeActivation.TechnicalDetails);

            var activation = _policy.ActivateScheme(snapshot.SchemeId);
            if (!activation.IsSuccess)
                return RestoreAfterFailure(snapshot, includeBattery,
                    "電源設定を反映できませんでした。", activation.TechnicalDetails);

            var activeAfter = IsStillActive(snapshot.SchemeId);
            if (!activeAfter.IsSuccess)
                return RestoreAfterFailure(snapshot, includeBattery,
                    activeAfter.UserMessage, activeAfter.TechnicalDetails);

            var verified = ReadSettings(snapshot.SchemeId, snapshot.Settings.HasBattery);
            if (!verified.IsSuccess || verified.Value is null)
                return RestoreAfterFailure(snapshot, includeBattery,
                    "変更後の電源設定を確認できませんでした。", verified.TechnicalDetails);

            if (!MatchesAppliedValues(verified.Value, value, includeBattery))
                return RestoreAfterFailure(snapshot, includeBattery,
                    "電源設定がWindowsに反映されませんでした。",
                    DescribeMismatch(verified.Value, value, includeBattery));

            _logger.Info("電源設定を変更し、反映結果を確認しました。");
            return OperationResult.Success("電源設定を適用しました。");
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private OperationResult<PowerSettingsSnapshot> GetSnapshot()
    {
        var active = _policy.GetActiveScheme();
        if (!active.IsSuccess)
            return OperationResult<PowerSettingsSnapshot>.Failure(
                "電源設定を取得できませんでした。Windowsの設定画面で確認してください。",
                active.TechnicalDetails);

        var settings = ReadSettings(active.Value, _hasBattery());
        if (!settings.IsSuccess || settings.Value is null)
            return OperationResult<PowerSettingsSnapshot>.Failure(
                "電源設定を取得できませんでした。Windowsの設定画面で確認してください。",
                settings.TechnicalDetails);

        return OperationResult<PowerSettingsSnapshot>.Success(
            new PowerSettingsSnapshot(active.Value, settings.Value));
    }

    private OperationResult<PowerSettings> ReadSettings(Guid schemeId, bool hasBattery)
    {
        var acDisplay = _policy.ReadValue(schemeId, VideoSubgroupId, DisplayTimeoutId, PowerSource.Ac);
        var acSleep = _policy.ReadValue(schemeId, SleepSubgroupId, SleepTimeoutId, PowerSource.Ac);
        var dcDisplay = _policy.ReadValue(schemeId, VideoSubgroupId, DisplayTimeoutId, PowerSource.Dc);
        var dcSleep = _policy.ReadValue(schemeId, SleepSubgroupId, SleepTimeoutId, PowerSource.Dc);
        var failure = new[] { acDisplay, acSleep, dcDisplay, dcSleep }.FirstOrDefault(result => !result.IsSuccess);
        if (failure is not null)
            return OperationResult<PowerSettings>.Failure(
                "電源設定の値を読み取れませんでした。", failure.TechnicalDetails);

        return OperationResult<PowerSettings>.Success(new PowerSettings(
            hasBattery, acDisplay.Value, acSleep.Value, dcDisplay.Value, dcSleep.Value));
    }

    private OperationResult IsStillActive(Guid expectedSchemeId)
    {
        var active = _policy.GetActiveScheme();
        if (!active.IsSuccess)
            return OperationResult.Failure(
                "有効な電源プランを再確認できなかったため、処理を中止しました。",
                active.TechnicalDetails);
        return active.Value == expectedSchemeId
            ? OperationResult.Success("")
            : OperationResult.Failure(
                "操作中に電源プランが切り替わったため、変更を中止しました。");
    }

    private OperationResult RestoreAfterFailure(
        PowerSettingsSnapshot original, bool includeBattery,
        string failureMessage, string? failureDetails)
    {
        _logger.Error(failureMessage);
        var rollback = Restore(original, includeBattery);
        if (rollback.IsSuccess)
        {
            _logger.Info("途中まで変更された電源設定を元の値へ戻しました。");
            var localizedFailure = L.T(failureMessage);
            var restored = L.T("変更前の値へ戻しました。");
            var userMessage = localizedFailure.Contains(restored, StringComparison.Ordinal)
                ? localizedFailure
                : $"{localizedFailure} {restored}";
            return new OperationResult(false, userMessage, failureDetails);
        }

        _logger.Error("途中まで変更された電源設定を完全には元へ戻せませんでした。");
        return OperationResult.Failure(
            "電源設定の変更に失敗し、元の値へ完全には戻せませんでした。Windowsの設定画面で確認してください。",
            $"{failureDetails}; 復元: {rollback.TechnicalDetails}");
    }

    private OperationResult Restore(PowerSettingsSnapshot original, bool includeBattery)
    {
        var errors = new List<string>();
        foreach (var change in BuildChanges(original.Settings, includeBattery))
        {
            var result = _policy.WriteValue(
                original.SchemeId, change.SubgroupId, change.SettingId,
                change.Source, change.Seconds);
            if (!result.IsSuccess)
                errors.Add(result.TechnicalDetails ?? result.UserMessage);
        }

        var active = _policy.GetActiveScheme();
        if (!active.IsSuccess)
            errors.Add(active.TechnicalDetails ?? active.UserMessage);
        else if (active.Value == original.SchemeId)
        {
            var activation = _policy.ActivateScheme(original.SchemeId);
            if (!activation.IsSuccess)
                errors.Add(activation.TechnicalDetails ?? activation.UserMessage);
        }

        var restored = ReadSettings(original.SchemeId, original.Settings.HasBattery);
        if (!restored.IsSuccess || restored.Value is null)
            errors.Add(restored.TechnicalDetails ?? restored.UserMessage);
        else if (!MatchesAppliedValues(restored.Value, original.Settings, includeBattery))
            errors.Add($"Rollback verification failed: {DescribeMismatch(
                restored.Value, original.Settings, includeBattery)}");

        return errors.Count == 0
            ? OperationResult.Success("")
            : OperationResult.Failure("電源設定を元へ戻せませんでした。", string.Join("; ", errors));
    }

    private static bool IsValid(PowerSettings value) =>
        value.AcDisplaySeconds <= MaximumTimeoutSeconds &&
        value.AcSleepSeconds <= MaximumTimeoutSeconds &&
        value.DcDisplaySeconds <= MaximumTimeoutSeconds &&
        value.DcSleepSeconds <= MaximumTimeoutSeconds;

    private static List<PowerChange> BuildChanges(PowerSettings value, bool includeBattery)
    {
        var changes = new List<PowerChange>
        {
            new(VideoSubgroupId, DisplayTimeoutId, PowerSource.Ac, value.AcDisplaySeconds, "AC display"),
            new(SleepSubgroupId, SleepTimeoutId, PowerSource.Ac, value.AcSleepSeconds, "AC sleep")
        };
        if (includeBattery)
        {
            changes.Add(new PowerChange(
                VideoSubgroupId, DisplayTimeoutId, PowerSource.Dc, value.DcDisplaySeconds, "DC display"));
            changes.Add(new PowerChange(
                SleepSubgroupId, SleepTimeoutId, PowerSource.Dc, value.DcSleepSeconds, "DC sleep"));
        }
        return changes;
    }

    private static bool MatchesAppliedValues(
        PowerSettings actual, PowerSettings expected, bool includeBattery) =>
        actual.AcDisplaySeconds == expected.AcDisplaySeconds &&
        actual.AcSleepSeconds == expected.AcSleepSeconds &&
        (!includeBattery ||
         (actual.DcDisplaySeconds == expected.DcDisplaySeconds &&
          actual.DcSleepSeconds == expected.DcSleepSeconds));

    private static string DescribeMismatch(
        PowerSettings actual, PowerSettings expected, bool includeBattery)
    {
        var expectedValues = BuildChanges(expected, includeBattery);
        var actualValues = BuildChanges(actual, includeBattery);
        return string.Join(", ", expectedValues.Zip(actualValues,
            (wanted, found) => $"{wanted.Name}: expected={wanted.Seconds}, actual={found.Seconds}"));
    }

    private static bool HasBattery()
    {
        return GetSystemPowerStatus(out var status)
               && status.BatteryFlag != 255
               && (status.BatteryFlag & 128) == 0;
    }

    private sealed record PowerSettingsSnapshot(Guid SchemeId, PowerSettings Settings);
    private sealed record PowerChange(
        Guid SubgroupId, Guid SettingId, PowerSource Source, uint Seconds, string Name);

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
}
