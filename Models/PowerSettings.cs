namespace WinBridge.Models;

public sealed record PowerSettings(
    bool HasBattery,
    int AcDisplayMinutes,
    int AcSleepMinutes,
    int DcDisplayMinutes,
    int DcSleepMinutes);

public sealed record PowerChoice(int Minutes, string Label)
{
    public override string ToString() => Label;
}

public sealed class PowerPresetSettings
{
    public int AcDisplayMinutes { get; set; } = 10;
    public int AcSleepMinutes { get; set; } = 30;
    public int DcDisplayMinutes { get; set; } = 10;
    public int DcSleepMinutes { get; set; } = 30;
}
