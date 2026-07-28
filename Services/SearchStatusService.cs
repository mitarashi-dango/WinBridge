using System.Runtime.InteropServices;
using WinBridge.Models;

namespace WinBridge.Services;

public sealed class SearchStatusService
{
    private const uint ScManagerConnect = 0x0001;
    private const uint ServiceQueryStatus = 0x0004;
    private const int ScStatusProcessInfo = 0;
    private const uint ServiceRunning = 4;
    private readonly LoggingService _logger;

    public SearchStatusService(LoggingService logger) => _logger = logger;

    public Task<OperationResult<string>> GetStatusAsync()
    {
        IntPtr manager = IntPtr.Zero;
        IntPtr service = IntPtr.Zero;
        try
        {
            manager = OpenSCManagerW(null, null, ScManagerConnect);
            if (manager == IntPtr.Zero)
                return Task.FromResult(Failure("サービス管理画面へ接続できませんでした。"));
            service = OpenServiceW(manager, "WSearch", ServiceQueryStatus);
            if (service == IntPtr.Zero)
                return Task.FromResult(Failure("Windows Searchサービスを開けませんでした。"));

            var status = new ServiceStatusProcess();
            var size = Marshal.SizeOf<ServiceStatusProcess>();
            if (!QueryServiceStatusEx(service, ScStatusProcessInfo, ref status, size, out _))
                return Task.FromResult(Failure("Windows Searchサービスの状態を取得できませんでした。"));

            _logger.Info("Windows Searchサービスの状態を取得しました。");
            var message = status.CurrentState == ServiceRunning
                ? "Windows Searchサービス：実行中"
                : "Windows Searchサービス：停止中または確認が必要";
            return Task.FromResult(OperationResult<string>.Success(message));
        }
        catch (Exception ex)
        {
            _logger.Error("Windows Searchサービスの状態を取得できませんでした。", ex);
            return Task.FromResult(OperationResult<string>.Failure(
                "Windows Searchサービス：状態を取得できません",
                $"{ex.GetType().Name}: {ex.Message}"));
        }
        finally
        {
            if (service != IntPtr.Zero) CloseServiceHandle(service);
            if (manager != IntPtr.Zero) CloseServiceHandle(manager);
        }
    }

    private OperationResult<string> Failure(string details)
    {
        var error = Marshal.GetLastWin32Error();
        _logger.Error($"{details} Win32Error: {error}");
        return OperationResult<string>.Failure(
            "Windows Searchサービス：状態を取得できません",
            $"{details} Win32Error: {error}");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatusProcess
    {
        public uint ServiceType;
        public uint CurrentState;
        public uint ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
        public uint ProcessId;
        public uint ServiceFlags;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenSCManagerW(string? machineName, string? databaseName, uint desiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenServiceW(IntPtr manager, string serviceName, uint desiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool QueryServiceStatusEx(
        IntPtr service, int infoLevel, ref ServiceStatusProcess buffer, int bufferSize, out int bytesNeeded);

    [DllImport("advapi32.dll")]
    private static extern bool CloseServiceHandle(IntPtr handle);
}
