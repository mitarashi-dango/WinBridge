using System.Runtime.InteropServices;
using WinBridge.Models;

namespace WinBridge.Services;

public sealed record DeviceStatus(int PresentDeviceCount, int ProblemDeviceCount);

public sealed class DeviceStatusService
{
    private const uint CrSuccess = 0;
    private const uint DnHasProblem = 0x00000400;
    private readonly LoggingService _logger;

    public DeviceStatusService(LoggingService logger) => _logger = logger;

    public OperationResult<DeviceStatus> GetStatus()
    {
        try
        {
            char[]? buffer = null;
            uint listResult = 1;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var sizeResult = CM_Get_Device_ID_List_SizeW(out var bufferLength, null, 0);
                if (sizeResult != CrSuccess || bufferLength == 0)
                    return Failure("デバイス一覧の大きさを取得できませんでした。", sizeResult);
                buffer = new char[bufferLength];
                listResult = CM_Get_Device_ID_ListW(null, buffer, bufferLength, 0);
                if (listResult == CrSuccess) break;
            }
            if (listResult != CrSuccess || buffer is null)
                return Failure("デバイス一覧を取得できませんでした。", listResult);

            var deviceIds = new string(buffer)
                .Split('\0', StringSplitOptions.RemoveEmptyEntries);
            var presentCount = 0;
            var problemCount = 0;

            foreach (var deviceId in deviceIds)
            {
                // NORMAL指定で現在のデバイスツリーに存在するものだけを対象にする。
                if (CM_Locate_DevNodeW(out var deviceInstance, deviceId, 0) != CrSuccess)
                    continue;
                presentCount++;
                if (CM_Get_DevNode_Status(out var status, out var problemNumber, deviceInstance, 0) == CrSuccess &&
                    (status & DnHasProblem) != 0 && problemNumber != 0)
                    problemCount++;
            }

            _logger.Info($"デバイス状態を取得しました。現在のデバイス: {presentCount}, 問題あり: {problemCount}");
            return OperationResult<DeviceStatus>.Success(new DeviceStatus(presentCount, problemCount));
        }
        catch (Exception ex)
        {
            _logger.Error("デバイス状態を取得できませんでした。", ex);
            return OperationResult<DeviceStatus>.Failure(
                "デバイスの状態を取得できませんでした。デバイスマネージャーで確認してください。",
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private OperationResult<DeviceStatus> Failure(string message, uint result)
    {
        _logger.Error($"{message} CONFIGRET: {result}");
        return OperationResult<DeviceStatus>.Failure(
            "デバイスの状態を取得できませんでした。デバイスマネージャーで確認してください。",
            $"CONFIGRET: {result}");
    }

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_ID_List_SizeW(
        out uint length, string? filter, uint flags);

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_ID_ListW(
        string? filter, [Out] char[] buffer, uint bufferLength, uint flags);

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Locate_DevNodeW(
        out uint deviceInstance, string deviceId, uint flags);

    [DllImport("CfgMgr32.dll")]
    private static extern uint CM_Get_DevNode_Status(
        out uint status, out uint problemNumber, uint deviceInstance, uint flags);
}
