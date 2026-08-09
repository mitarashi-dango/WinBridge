using System.Collections.ObjectModel;
using System.Windows;
using WinBridge.Models;
using WinBridge.Services;

namespace WinBridge.ViewModels;

public sealed class PowerViewModel : ObservableObject
{
    private readonly PowerSettingsService _service;
    private readonly PowerPresetService _presetService;
    private readonly Action<OperationResult> _report;
    private bool _hasBattery;
    private bool _isBusy;
    private PowerChoice? _acDisplay, _acSleep, _dcDisplay, _dcSleep;

    public ObservableCollection<PowerChoice> DisplayChoices { get; } =
        CreateChoices([1, 3, 5, 10, 15, 30, 60, 120, 0]);
    public ObservableCollection<PowerChoice> SleepChoices { get; } =
        CreateChoices([1, 3, 5, 10, 15, 30, 60, 120, 180, 0]);
    public bool HasBattery { get => _hasBattery; set => SetProperty(ref _hasBattery, value); }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            OnPropertyChanged(nameof(IsInteractionEnabled));
            RefreshCommand.RaiseCanExecuteChanged();
            ApplyCommand.RaiseCanExecuteChanged();
            SaveCustomPresetCommand.RaiseCanExecuteChanged();
            PresetCommand.RaiseCanExecuteChanged();
        }
    }
    public bool IsInteractionEnabled => !IsBusy;
    public PowerChoice? AcDisplay { get => _acDisplay; set => SetProperty(ref _acDisplay, value); }
    public PowerChoice? AcSleep { get => _acSleep; set => SetProperty(ref _acSleep, value); }
    public PowerChoice? DcDisplay { get => _dcDisplay; set => SetProperty(ref _dcDisplay, value); }
    public PowerChoice? DcSleep { get => _dcSleep; set => SetProperty(ref _dcSleep, value); }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ApplyCommand { get; }
    public AsyncRelayCommand SaveCustomPresetCommand { get; }
    public RelayCommand PresetCommand { get; }

    public PowerViewModel(PowerSettingsService service, PowerPresetService presetService,
        Action<OperationResult> report)
    {
        _service = service;
        _presetService = presetService;
        _report = report;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        ApplyCommand = new AsyncRelayCommand(ApplyAsync, () => !IsBusy);
        SaveCustomPresetCommand = new AsyncRelayCommand(SaveCustomPresetAsync, () => !IsBusy);
        PresetCommand = new RelayCommand(
            p => SelectPreset(p?.ToString() ?? ""), _ => !IsBusy);
    }

    public async Task RefreshAsync()
    {
        if (!BeginOperation()) return;
        try
        {
            var result = await RefreshCoreAsync();
            _report(result.IsSuccess
                ? OperationResult.Success("Windowsの現在の電源設定を読み込みました。")
                : result);
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task<OperationResult> RefreshCoreAsync()
    {
        var result = await _service.GetAsync();
        if (!result.IsSuccess || result.Value is null)
            return OperationResult.Failure(result.UserMessage, result.TechnicalDetails);

        RemoveCurrentChoices(DisplayChoices);
        RemoveCurrentChoices(SleepChoices);
        HasBattery = result.Value.HasBattery;
        AcDisplay = Choice(DisplayChoices, result.Value.AcDisplaySeconds, true);
        AcSleep = Choice(SleepChoices, result.Value.AcSleepSeconds, true);
        DcDisplay = Choice(DisplayChoices, result.Value.DcDisplaySeconds, true);
        DcSleep = Choice(SleepChoices, result.Value.DcSleepSeconds, true);
        return OperationResult.Success("");
    }

    private async Task ApplyAsync()
    {
        if (!BeginOperation()) return;
        try
        {
            if (AcDisplay is null || AcSleep is null ||
                (HasBattery && (DcDisplay is null || DcSleep is null)))
            {
                _report(OperationResult.Failure("すべての時間を選択してください。"));
                return;
            }
            if (NeedsWarning(AcDisplay.Seconds, AcSleep.Seconds) ||
                (HasBattery && NeedsWarning(DcDisplay!.Seconds, DcSleep!.Seconds)))
            {
                var answer = MessageBox.Show(
                    L.T("スリープが画面OFFより先に実行されるため、画面OFF設定は通常使用されません。\n\nこの設定を適用しますか？"),
                    L.T("設定の確認"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (answer != MessageBoxResult.Yes) return;
            }

            var value = new PowerSettings(
                HasBattery, AcDisplay.Seconds, AcSleep.Seconds,
                DcDisplay?.Seconds ?? 0, DcSleep?.Seconds ?? 0);
            var result = await _service.ApplyAsync(value);
            if (!result.IsSuccess)
            {
                _report(result);
                return;
            }

            var refreshed = await RefreshCoreAsync();
            _report(refreshed.IsSuccess
                ? result
                : OperationResult.Failure(
                    "電源設定は適用されましたが、画面の現在値を再読み込みできませんでした。",
                    refreshed.TechnicalDetails));
        }
        finally
        {
            EndOperation();
        }
    }

    private void SelectPreset(string preset)
    {
        if (preset == "custom")
        {
            var custom = _presetService.Get();
            if (!TryPresetSeconds(custom.AcDisplayMinutes, out var acDisplay) ||
                !TryPresetSeconds(custom.AcSleepMinutes, out var acSleep) ||
                !TryPresetSeconds(custom.DcDisplayMinutes, out var dcDisplay) ||
                !TryPresetSeconds(custom.DcSleepMinutes, out var dcSleep))
            {
                _report(OperationResult.Failure(
                    "お好みプリセットの時間が正しくないため読み込めませんでした。"));
                return;
            }
            AcDisplay = Choice(DisplayChoices, acDisplay);
            AcSleep = Choice(SleepChoices, acSleep);
            DcDisplay = Choice(DisplayChoices, dcDisplay);
            DcSleep = Choice(SleepChoices, dcSleep);
        }
        else
        {
            var (display, sleep) = preset switch
            {
                "focus" => (0, 0),
                "saving" => (3, 5),
                _ => (10, 30)
            };
            _ = TryPresetSeconds(display, out var displaySeconds);
            _ = TryPresetSeconds(sleep, out var sleepSeconds);
            AcDisplay = Choice(DisplayChoices, displaySeconds);
            AcSleep = Choice(SleepChoices, sleepSeconds);
            DcDisplay = Choice(DisplayChoices, displaySeconds);
            DcSleep = Choice(SleepChoices, sleepSeconds);
        }
        _report(OperationResult.Success(
            "プリセットを選択しました。「設定を適用」するまでWindowsには反映されません。"));
    }

    private async Task SaveCustomPresetAsync()
    {
        if (!BeginOperation()) return;
        try
        {
            if (AcDisplay is null || AcSleep is null)
            {
                _report(OperationResult.Failure("すべての時間を選択してください。"));
                return;
            }

            var dcDisplay = DcDisplay?.Seconds ?? AcDisplay.Seconds;
            var dcSleep = DcSleep?.Seconds ?? AcSleep.Seconds;
            if (!TryPresetMinutes(AcDisplay.Seconds, out var acDisplayMinutes) ||
                !TryPresetMinutes(AcSleep.Seconds, out var acSleepMinutes) ||
                !TryPresetMinutes(dcDisplay, out var dcDisplayMinutes) ||
                !TryPresetMinutes(dcSleep, out var dcSleepMinutes))
            {
                _report(OperationResult.Failure(
                    "1分未満の現在値はお好みプリセットへ保存できません。分単位の時間を選択してください。"));
                return;
            }

            var preset = new PowerPresetSettings
            {
                AcDisplayMinutes = acDisplayMinutes,
                AcSleepMinutes = acSleepMinutes,
                DcDisplayMinutes = dcDisplayMinutes,
                DcSleepMinutes = dcSleepMinutes
            };
            var result = await _presetService.SaveAsync(preset);
            _report(result.IsSuccess
                ? OperationResult.Success("現在の選択を「お好み」プリセットへ保存しました。")
                : result);
        }
        finally
        {
            EndOperation();
        }
    }

    private bool BeginOperation()
    {
        if (IsBusy) return false;
        IsBusy = true;
        return true;
    }

    private void EndOperation() => IsBusy = false;

    private static bool NeedsWarning(uint display, uint sleep) =>
        sleep > 0 && (display == 0 || sleep < display);

    private static PowerChoice Choice(
        ObservableCollection<PowerChoice> choices, uint seconds, bool isCurrentValue = false)
    {
        var existing = choices.FirstOrDefault(choice => choice.Seconds == seconds);
        if (existing is not null) return existing;

        var custom = new PowerChoice(seconds, FormatTime(seconds, isCurrentValue), isCurrentValue);
        choices.Insert(choices.Count - 1, custom);
        return custom;
    }

    private static string FormatTime(uint seconds, bool isCurrentValue)
    {
        var suffix = isCurrentValue ? L.T("（現在値）") : "";
        if (seconds == 0) return L.T("なし");
        if (seconds % 60 == 0)
            return L.F("{0}分{1}", seconds / 60, suffix);
        return L.F("{0}秒{1}", seconds, suffix);
    }

    private static void RemoveCurrentChoices(ObservableCollection<PowerChoice> choices)
    {
        foreach (var choice in choices.Where(choice => choice.IsCurrentValue).ToArray())
            choices.Remove(choice);
    }

    private static bool TryPresetSeconds(int minutes, out uint seconds)
    {
        if (minutes < 0 || minutes > PowerPresetService.MaximumMinutes)
        {
            seconds = 0;
            return false;
        }
        seconds = (uint)minutes * 60u;
        return true;
    }

    private static bool TryPresetMinutes(uint seconds, out int minutes)
    {
        if (seconds % 60 != 0 || seconds / 60 > PowerPresetService.MaximumMinutes)
        {
            minutes = 0;
            return false;
        }
        minutes = (int)(seconds / 60);
        return true;
    }

    private static ObservableCollection<PowerChoice> CreateChoices(IEnumerable<int> values) =>
        new(values.Select(minutes => new PowerChoice((uint)minutes * 60u, minutes switch
        {
            0 => L.T("なし"), 60 => L.T("1時間"), 120 => L.T("2時間"), 180 => L.T("3時間"),
            _ => L.F("{0}分", minutes)
        })));
}
