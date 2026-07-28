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
