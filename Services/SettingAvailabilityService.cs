using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using WinBridge.Models;

namespace WinBridge.Services;

public interface ISettingAvailabilityService
{
    bool IsAvailable(SettingAvailability availability);
}

public sealed class SettingAvailabilityService : ISettingAvailabilityService
{
    private const int SmDigitizer = 94;
    private const int SmMaximumTouches = 95;
    private const int SmMonitors = 80;
    private const int NidIntegratedTouch = 0x01;
    private const int NidIntegratedPen = 0x02;
    private const int NidExternalTouch = 0x04;
    private const int NidExternalPen = 0x08;
    private const int NidReady = 0x80;
    private const uint CrSuccess = 0;
    private const uint CmDrpDeviceDesc = 1;
    private const uint CmDrpFriendlyName = 13;

    private readonly Lazy<string> _deviceText = new(ReadPresentDeviceText);

    public bool IsAvailable(SettingAvailability availability)
    {
        try
        {
            return availability switch
            {
                SettingAvailability.Always => true,
                SettingAvailability.Battery => HasBattery(),
                SettingAvailability.Touch => HasDigitizer(NidIntegratedTouch | NidExternalTouch) ||
                                             GetSystemMetrics(SmMaximumTouches) > 0,
                SettingAvailability.Touchpad => HasPrecisionTouchpad() ||
                                                HasDevice("TOUCHPAD", "CLICKPAD", "HID_DEVICE_UP:000D_U:0005"),
                SettingAvailability.Pen => HasDigitizer(NidIntegratedPen | NidExternalPen),
                SettingAvailability.SurfaceDial => HasDevice("SURFACE DIAL", "VID_045E&PID_091B"),
                SettingAvailability.EyeTracker => HasDevice("EYE TRACKER", "EYETRACKER", "TOBII"),
                SettingAvailability.HearingDevice => HasDevice("HEARING AID", "HEARINGAID", "LE AUDIO"),
                SettingAvailability.Cellular => HasDevice("WWAN", "MOBILE BROADBAND", "CELLULAR", "MBIM"),
                SettingAvailability.DirectAccess => HasDirectAccessPolicy(),
                SettingAvailability.AdvancedDisplay => GetSystemMetrics(SmMonitors) > 0,
                SettingAvailability.Graphics => GetSystemMetrics(SmMonitors) > 0,
                SettingAvailability.PresenceSensing => HasDevice(
                    "HUMAN PRESENCE", "PRESENCE SENSOR", "SENSOR PRESENCE"),
                SettingAvailability.WindowsInsider => HasWindowsInsiderEnrollment(),
                SettingAvailability.WindowsHelloFace => HasDevice(
                    "WINDOWS HELLO FACE", "IR CAMERA", "BIOMETRIC FACE"),
                SettingAvailability.WindowsHelloFingerprint => HasDevice(
                    "FINGERPRINT", "BIOMETRIC COPROCESSOR"),
                SettingAvailability.SecurityKey => HasDevice("FIDO", "U2F", "SECURITY KEY"),
                SettingAvailability.DynamicLighting => HasDevice("LAMPARRAY", "DYNAMIC LIGHTING"),
                SettingAvailability.CopilotKey => HasDevice("COPILOT KEY") || HasCopilotKeyConfiguration(),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    private bool HasDevice(params string[] tokens)
    {
        var deviceText = _deviceText.Value;
        return tokens.Any(token => deviceText.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasDigitizer(int capability)
    {
        var digitizer = GetSystemMetrics(SmDigitizer);
        return (digitizer & NidReady) != 0 && (digitizer & capability) != 0;
    }

    private static bool HasBattery()
    {
        if (!GetSystemPowerStatus(out var status)) return false;
        return status.BatteryFlag != 128 && status.BatteryFlag != 255;
    }

    private static bool HasPrecisionTouchpad() =>
        ReadRegistryDword(
            Registry.LocalMachine,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\PrecisionTouchPad\Status",
            "Enabled") == 1;

    private static bool HasDirectAccessPolicy()
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Policies\Microsoft\Windows\TCPIP\v6Transition");
        return key is not null;
    }

    private static bool HasWindowsInsiderEnrollment()
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\WindowsSelfHost\Applicability");
        return key?.GetValue("BranchName") is string branch && !string.IsNullOrWhiteSpace(branch);
    }

    private static bool HasCopilotKeyConfiguration()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Input\Settings\CopilotKey");
        return key is not null;
    }

    private static int? ReadRegistryDword(RegistryKey root, string path, string valueName)
    {
        try
        {
            using var key = root.OpenSubKey(path);
            return key?.GetValue(valueName) as int?;
        }
        catch
        {
            return null;
        }
    }

    private static string ReadPresentDeviceText()
    {
        try
        {
            if (CM_Get_Device_ID_List_SizeW(out var bufferLength, null, 0) != CrSuccess ||
                bufferLength == 0)
                return "";

            var buffer = new char[bufferLength];
            if (CM_Get_Device_ID_ListW(null, buffer, bufferLength, 0) != CrSuccess)
                return "";

            var builder = new StringBuilder();
            foreach (var deviceId in new string(buffer)
                         .Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                builder.AppendLine(deviceId);
                if (CM_Locate_DevNodeW(out var deviceInstance, deviceId, 0) != CrSuccess)
                    continue;
                builder.AppendLine(ReadDeviceProperty(deviceInstance, CmDrpFriendlyName));
                builder.AppendLine(ReadDeviceProperty(deviceInstance, CmDrpDeviceDesc));
            }
            return builder.ToString();
        }
        catch
        {
            return "";
        }
    }

    private static string ReadDeviceProperty(uint deviceInstance, uint property)
    {
        var buffer = new byte[1024];
        var length = (uint)buffer.Length;
        if (CM_Get_DevNode_Registry_PropertyW(
                deviceInstance, property, out _, buffer, ref length, 0) != CrSuccess ||
            length < 2)
            return "";
        return Encoding.Unicode.GetString(buffer, 0, (int)length).TrimEnd('\0');
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

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_ID_List_SizeW(
        out uint length, string? filter, uint flags);

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_ID_ListW(
        string? filter, [Out] char[] buffer, uint bufferLength, uint flags);

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Locate_DevNodeW(
        out uint deviceInstance, string deviceId, uint flags);

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_DevNode_Registry_PropertyW(
        uint deviceInstance,
        uint property,
        out uint registryDataType,
        [Out] byte[] buffer,
        ref uint bufferLength,
        uint flags);
}
