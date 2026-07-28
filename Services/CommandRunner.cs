using System.Diagnostics;
using WinBridge.Models;

namespace WinBridge.Services;

internal static class CommandRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    public static async Task<OperationResult<string>> RunAsync(string fileName, params string[] arguments)
        => await RunAsync(fileName, DefaultTimeout, arguments);

    public static async Task<OperationResult<string>> RunAsync(
        string fileName, TimeSpan timeout, params string[] arguments)
    {
        try
        {
            var info = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var argument in arguments) info.ArgumentList.Add(argument);
            using var process = Process.Start(info);
            if (process is null) return OperationResult<string>.Failure("Windowsの処理を開始できませんでした。");
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            using var timeoutSource = new CancellationTokenSource(timeout);
            try
            {
                await process.WaitForExitAsync(timeoutSource.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(true);
                    await process.WaitForExitAsync();
                    await Task.WhenAll(outputTask, errorTask);
                }
                catch { }
                return OperationResult<string>.Failure(
                    "Windowsの処理が時間内に完了しなかったため中止しました。",
                    $"処理: {Path.GetFileName(fileName)}; タイムアウト: {timeout.TotalSeconds:0}秒");
            }
            var output = await outputTask;
            var error = await errorTask;
            return process.ExitCode == 0
                ? OperationResult<string>.Success(output)
                : OperationResult<string>.Failure("Windowsの処理が完了しませんでした。",
                    $"終了コード: {process.ExitCode}; {error.Trim()}");
        }
        catch (Exception ex)
        {
            return OperationResult<string>.Failure("Windowsの処理を実行できませんでした。",
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
