using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace WinBridge.Services;

internal enum ExplorerValue { HideFileExt, Hidden }

internal sealed record ExplorerRegistryValue(object Value, RegistryValueKind Kind);

internal sealed record ExplorerSettingsSnapshot(
    ExplorerRegistryValue? HideFileExt, ExplorerRegistryValue? Hidden)
{
    public ExplorerSettings ToSettings() => new(
        Convert.ToInt32(HideFileExt?.Value ?? 1) == 0,
        Convert.ToInt32(Hidden?.Value ?? 2) == 1);

    public static ExplorerSettingsSnapshot FromSettings(bool showExtensions, bool showHidden) => new(
        new ExplorerRegistryValue(showExtensions ? 0 : 1, RegistryValueKind.DWord),
        new ExplorerRegistryValue(showHidden ? 1 : 2, RegistryValueKind.DWord));
}

internal interface IExplorerSettingsAccessor
{
    ExplorerSettingsSnapshot Read();
    void WriteValue(ExplorerValue name, ExplorerRegistryValue? value);
    void NotifyShell();
}

internal sealed class ExplorerSettingsAccessor : IExplorerSettingsAccessor
{
    private const string AdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    public ExplorerSettingsSnapshot Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(AdvancedKey);
        return new(ReadValue(key, ExplorerValue.HideFileExt), ReadValue(key, ExplorerValue.Hidden));
    }

    public void WriteValue(ExplorerValue name, ExplorerRegistryValue? value)
    {
        if (value is null)
        {
            using var existing = Registry.CurrentUser.OpenSubKey(AdvancedKey, true);
            existing?.DeleteValue(name.ToString(), false);
        }
        else
        {
            using var key = Registry.CurrentUser.CreateSubKey(AdvancedKey, true);
            key.SetValue(name.ToString(), value.Value, value.Kind);
        }
    }

    public void NotifyShell() => SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

    private static ExplorerRegistryValue? ReadValue(RegistryKey? key, ExplorerValue name)
    {
        var value = key?.GetValue(name.ToString(), null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value is null ? null : new ExplorerRegistryValue(value, key!.GetValueKind(name.ToString()));
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
}
