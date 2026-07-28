using System.Collections.ObjectModel;
using System.Windows;
using WinBridge.Models;
using WinBridge.Services;

namespace WinBridge.ViewModels;

public sealed class PowerViewModel : ObservableObject
{
    private readonly PowerSettingsService _service;
    private readonly Action<OperationResult> _report;
    private bool _hasBattery;
    private PowerChoice? _acDisplay, _acSleep, _dcDisplay, _dcSleep;

    public ObservableCollection<PowerChoice> DisplayChoices { get; } = CreateChoices([1, 3, 5, 10, 15, 30, 60, 120, 0]);
    public ObservableCollection<PowerChoice> SleepChoices { get; } = CreateChoices([1, 3, 5, 10, 15, 30, 60, 120, 180, 0]);
    public bool HasBattery { get => _hasBattery; set => SetProperty(ref _hasBattery, value); }
    public PowerChoice? AcDisplay { get => _acDisplay; set => SetProperty(ref _acDisplay, value); }
    public PowerChoice? AcSleep { get => _acSleep; set => SetProperty(ref _acSleep, value); }
    public PowerChoice? DcDisplay { get => _dcDisplay; set => SetProperty(ref _dcDisplay, value); }
    public PowerChoice? DcSleep { get => _dcSleep; set => SetProperty(ref _dcSleep, value); }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ApplyCommand { get; }
    public RelayCommand PresetCommand { get; }

    public PowerViewModel(PowerSettingsService service, Action<OperationResult> report)
    {
        _service = service;
        _report = report;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ApplyCommand = new AsyncRelayCommand(ApplyAsync);
        PresetCommand = new RelayCommand(p => SelectPreset(p?.ToString() ?? ""));
    }

    public async Task RefreshAsync()
    {
        var result = await _service.GetAsync();
        if (!result.IsSuccess || result.Value is null)
        {
            _report(OperationResult.Failure(result.UserMessage, result.TechnicalDetails));
            return;
        }
        HasBattery = result.Value.HasBattery;
        AcDisplay = Choice(DisplayChoices, result.Value.AcDisplayMinutes);
        AcSleep = Choice(SleepChoices, result.Value.AcSleepMinutes);
        DcDisplay = Choice(DisplayChoices, result.Value.DcDisplayMinutes);
        DcSleep = Choice(SleepChoices, result.Value.DcSleepMinutes);
        _report(OperationResult.Success("Windowsの現在の電源設定を読み込みました。"));
    }

    private async Task ApplyAsync()
    {
        if (AcDisplay is null || AcSleep is null || (HasBattery && (DcDisplay is null || DcSleep is null)))
        {
            _report(OperationResult.Success("すべての時間を選択してください。"));
            return;
        }
        if (NeedsWarning(AcDisplay.Minutes, AcSleep.Minutes) ||
            (HasBattery && NeedsWarning(DcDisplay!.Minutes, DcSleep!.Minutes)))
        {
            var answer = MessageBox.Show(
                "スリープが画面OFFより先に実行されるため、画面OFF設定は通常使用されません。\n\nこの設定を適用しますか？",
                "設定の確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;
        }
        var value = new PowerSettings(HasBattery, AcDisplay.Minutes, AcSleep.Minutes,
            DcDisplay?.Minutes ?? 0, DcSleep?.Minutes ?? 0);
        var result = await _service.ApplyAsync(value);
        _report(result);
        if (result.IsSuccess) await RefreshAsync();
    }

    private void SelectPreset(string preset)
    {
        var (display, sleep) = preset switch
        {
            "focus" => (0, 0),
            "saving" => (3, 5),
            _ => (10, 30)
        };
        AcDisplay = Choice(DisplayChoices, display);
        AcSleep = Choice(SleepChoices, sleep);
        DcDisplay = Choice(DisplayChoices, display);
        DcSleep = Choice(SleepChoices, sleep);
        _report(OperationResult.Success("プリセットを選択しました。「設定を適用」するまでWindowsには反映されません。"));
    }

    private static bool NeedsWarning(int display, int sleep) =>
        sleep > 0 && (display == 0 || sleep < display);

    private static PowerChoice Choice(ObservableCollection<PowerChoice> choices, int minutes)
    {
        var existing = choices.FirstOrDefault(c => c.Minutes == minutes);
        if (existing is not null) return existing;
        var custom = new PowerChoice(minutes, $"{minutes}分（現在値）");
        choices.Insert(choices.Count - 1, custom);
        return custom;
    }

    private static ObservableCollection<PowerChoice> CreateChoices(IEnumerable<int> values) =>
        new(values.Select(m => new PowerChoice(m, m switch
        {
            0 => "なし", 60 => "1時間", 120 => "2時間", 180 => "3時間", _ => $"{m}分"
        })));
}
