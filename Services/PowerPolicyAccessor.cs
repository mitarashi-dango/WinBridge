using System.ComponentModel;
using System.Runtime.InteropServices;
using WinBridge.Models;

namespace WinBridge.Services;

internal enum PowerSource
{
    Ac,
    Dc
}

internal interface IPowerPolicyAccessor
{
    OperationResult<Guid> GetActiveScheme();
    OperationResult<uint> ReadValue(Guid schemeId, Guid subgroupId, Guid settingId, PowerSource source);
    OperationResult WriteValue(Guid schemeId, Guid subgroupId, Guid settingId, PowerSource source, uint seconds);
    OperationResult ActivateScheme(Guid schemeId);
}

internal sealed class PowerPolicyAccessor : IPowerPolicyAccessor
{
    public OperationResult<Guid> GetActiveScheme()
    {
        IntPtr schemePointer = IntPtr.Zero;
        try
        {
            var status = PowerGetActiveScheme(IntPtr.Zero, out schemePointer);
            if (status != 0 || schemePointer == IntPtr.Zero)
                return OperationResult<Guid>.Failure(
                    "有効な電源プランを確認できませんでした。", ErrorDetails(status));

            return OperationResult<Guid>.Success(Marshal.PtrToStructure<Guid>(schemePointer));
        }
        catch (Exception ex)
        {
            return OperationResult<Guid>.Failure(
                "有効な電源プランを確認できませんでした。", ex.ToString());
        }
        finally
        {
            if (schemePointer != IntPtr.Zero)
                _ = LocalFree(schemePointer);
        }
    }

    public OperationResult<uint> ReadValue(
        Guid schemeId, Guid subgroupId, Guid settingId, PowerSource source)
    {
        try
        {
            var status = source == PowerSource.Ac
                ? PowerReadACValueIndex(IntPtr.Zero, ref schemeId, ref subgroupId, ref settingId, out var value)
                : PowerReadDCValueIndex(IntPtr.Zero, ref schemeId, ref subgroupId, ref settingId, out value);
            return status == 0
                ? OperationResult<uint>.Success(value)
                : OperationResult<uint>.Failure("電源設定の値を読み取れませんでした。", ErrorDetails(status));
        }
        catch (Exception ex)
        {
            return OperationResult<uint>.Failure("電源設定の値を読み取れませんでした。", ex.ToString());
        }
    }

    public OperationResult WriteValue(
        Guid schemeId, Guid subgroupId, Guid settingId, PowerSource source, uint seconds)
    {
        try
        {
            var status = source == PowerSource.Ac
                ? PowerWriteACValueIndex(IntPtr.Zero, ref schemeId, ref subgroupId, ref settingId, seconds)
                : PowerWriteDCValueIndex(IntPtr.Zero, ref schemeId, ref subgroupId, ref settingId, seconds);
            return status == 0
                ? OperationResult.Success("")
                : OperationResult.Failure("電源設定を書き込めませんでした。", ErrorDetails(status));
        }
        catch (Exception ex)
        {
            return OperationResult.Failure("電源設定を書き込めませんでした。", ex.ToString());
        }
    }

    public OperationResult ActivateScheme(Guid schemeId)
    {
        try
        {
            var status = PowerSetActiveScheme(IntPtr.Zero, ref schemeId);
            return status == 0
                ? OperationResult.Success("")
                : OperationResult.Failure("電源プランを更新できませんでした。", ErrorDetails(status));
        }
        catch (Exception ex)
        {
            return OperationResult.Failure("電源プランを更新できませんでした。", ex.ToString());
        }
    }

    private static string ErrorDetails(uint status) =>
        $"Power API error 0x{status:X8}: {new Win32Exception(unchecked((int)status)).Message}";

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadACValueIndex(
        IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subgroupGuid,
        ref Guid settingGuid, out uint valueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadDCValueIndex(
        IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subgroupGuid,
        ref Guid settingGuid, out uint valueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteACValueIndex(
        IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subgroupGuid,
        ref Guid settingGuid, uint valueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteDCValueIndex(
        IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subgroupGuid,
        ref Guid settingGuid, uint valueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
