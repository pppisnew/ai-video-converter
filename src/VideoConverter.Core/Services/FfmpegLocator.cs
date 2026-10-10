using VideoConverter.Core.Internal;

namespace VideoConverter.Core.Services;

/// <summary>ffmpeg 二进制的来源。</summary>
public enum FfmpegSource
{
    /// <summary>随应用/安装包捆绑（AppContext.BaseDirectory/tools/ffmpeg）。</summary>
    Bundled,

    /// <summary>仓库开发态 tools/ffmpeg（向上层目录查找）。</summary>
    Repository,

    /// <summary>系统 PATH。</summary>
    SystemPath,
}

public sealed record FfmpegBinaries(string FfmpegPath, string FfprobePath, FfmpegSource Source)
{
    public string Directory => System.IO.Path.GetDirectoryName(FfmpegPath)!;
}

public interface IFfmpegLocator
{
    /// <summary>按 捆绑 -> 仓库 -> PATH 的顺序定位 ffmpeg；都找不到返回 null。</summary>
    FfmpegBinaries? Locate();

    /// <summary>读取 ffmpeg 版本号（取 -version 首行）。</summary>
    string? GetVersion(FfmpegBinaries binaries);
}

public sealed class FfmpegLocator : IFfmpegLocator
{
    private const string ExeName = "ffmpeg.exe";
    private const string ProbeName = "ffprobe.exe";

    public FfmpegBinaries? Locate()
    {
        var fromBundle = ProbeDirectory(System.IO.Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg"), FfmpegSource.Bundled);
        if (fromBundle is not null)
        {
            return fromBundle;
        }

        foreach (var candidate in WalkUpForToolsDirectory(AppContext.BaseDirectory))
        {
            var found = ProbeDirectory(candidate, FfmpegSource.Repository);
            if (found is not null)
            {
                return found;
            }
        }

        var fromPath = ProbeSystemPath();
        return fromPath;
    }

    public string? GetVersion(FfmpegBinaries binaries)
    {
        try
        {
            var result = ProcessRunner.RunAsync(binaries.FfmpegPath, new[] { "-version" }).GetAwaiter().GetResult();
            var firstLine = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return firstLine?.Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static FfmpegBinaries? ProbeDirectory(string directory, FfmpegSource source)
    {
        var ffmpeg = System.IO.Path.Combine(directory, ExeName);
        var ffprobe = System.IO.Path.Combine(directory, ProbeName);
        return File.Exists(ffmpeg) && File.Exists(ffprobe)
            ? new FfmpegBinaries(ffmpeg, ffprobe, source)
            : null;
    }

    /// <summary>从基准目录逐级向上查找 tools/ffmpeg 目录（开发态运行位置不确定）。</summary>
    internal static IEnumerable<string> WalkUpForToolsDirectory(string baseDirectory)
    {
        var current = new DirectoryInfo(baseDirectory);
        for (var depth = 0; depth < 8 && current is not null; depth++)
        {
            yield return System.IO.Path.Combine(current.FullName, "tools", "ffmpeg");
            current = current.Parent;
        }
    }

    private FfmpegBinaries? ProbeSystemPath()
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVariable))
        {
            return null;
        }

        foreach (var entry in pathVariable.Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var expanded = Environment.ExpandEnvironmentVariables(entry);
            var found = ProbeDirectory(expanded, FfmpegSource.SystemPath);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
