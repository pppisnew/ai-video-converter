using System.Diagnostics;
using System.Text;

namespace VideoConverter.Core.Internal;

internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// 统一的外部进程执行器：逐参数传递（避免空格/中文路径踩坑），
/// 支持逐行读取 stdout 与取消时终止进程树。
/// </summary>
internal static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        Action<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"进程启动失败：{fileName}");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"无法启动 {fileName}：{ex.Message}", ex);
        }

        var stderrTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardError.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
            {
                lock (stderr)
                {
                    stderr.AppendLine(line);
                }
            }
        }, cancellationToken);

        try
        {
            string? outputLine;
            while ((outputLine = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
            {
                lock (stdout)
                {
                    stdout.AppendLine(outputLine);
                }

                onOutputLine?.Invoke(outputLine);
            }

            await stderrTask.ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKillProcessTree(process);
            throw;
        }

        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // 进程可能已自行退出，忽略
        }
    }

    /// <summary>取 stderr 末尾若干行作为错误摘要（ffmpeg 的报错信息在最后）。</summary>
    public static string SummarizeError(string stderr, int maxLines = 5)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return string.Empty;
        }

        var lines = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(Environment.NewLine, lines.TakeLast(maxLines));
    }
}
