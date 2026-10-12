using System.Security.Cryptography;
using System.Text;

namespace VideoConverter.Core.Services;

/// <summary>
/// 队列行缩略图的磁盘缓存。缓存文件名只由输入文件的完整路径推导，因此同一文件
/// 重复入队或应用重启后都会命中同一个缓存文件，不需要重新截帧。
/// </summary>
public static class ThumbnailCache
{
    /// <summary>缓存目录：<c>%LOCALAPPDATA%\VideoConverter\thumbs</c>。</summary>
    public static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VideoConverter",
        "thumbs");

    /// <summary>
    /// 缓存文件名：完整输入路径小写后的 SHA1 十六进制 + <c>.jpg</c>（40 位十六进制，共 44 字符）。
    /// 小写是为了让同一文件在磁盘大小写不敏感下也只有一份缓存。
    /// </summary>
    public static string FileNameFor(string inputPath) => HashFor(inputPath) + ".jpg";

    /// <summary>某个输入文件对应的缓存文件完整路径。</summary>
    public static string PathFor(string inputPath) =>
        Path.Combine(DirectoryPath, FileNameFor(inputPath));

    /// <summary>
    /// “重新截取”产物：<c>&lt;sha1&gt;_&lt;毫秒时间戳&gt;.jpg</c>。每次重新截取都写一个新文件，
    /// 行内 Image 换个路径就能重新解码（转换器按路径缓存已解码的图），也不会覆盖正在显示的那张。
    /// </summary>
    public static string FileNameForRecapture(string inputPath, long unixMilliseconds) =>
        $"{HashFor(inputPath)}_{unixMilliseconds}.jpg";

    /// <summary>某个输入文件的“重新截取”产物完整路径。</summary>
    public static string PathForRecapture(string inputPath, long unixMilliseconds) =>
        Path.Combine(DirectoryPath, FileNameForRecapture(inputPath, unixMilliseconds));

    /// <summary>
    /// 是否是稳定缓存名（40 位十六进制 + <c>.jpg</c>）。稳定缓存是入队时截的那张，
    /// 清理时一律保留；其余文件都是可再生的“重新截取”产物。
    /// </summary>
    public static bool IsStableFileName(string fileName)
    {
        if (fileName is null || fileName.Length != 44
            || !fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        for (var i = 0; i < 40; i++)
        {
            if (!Uri.IsHexDigit(fileName[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 删除缓存目录里超过 <paramref name="maxAge"/> 的“重新截取”文件，返回删除数量。
    /// 清理只是顺手做的事：单个文件删不掉（仍被 BitmapImage 占用 / 权限不足）或目录枚举失败，
    /// 一律吞掉，绝不冒给调用方，免得影响启动或截帧。
    /// </summary>
    public static int PruneStaleRecaptures(TimeSpan maxAge, DateTimeOffset? now = null) =>
        PruneDirectory(DirectoryPath, (now ?? DateTimeOffset.UtcNow) - maxAge);

    /// <summary>按目录清理的实体（<paramref name="directory"/> 可注入，便于测试）。</summary>
    internal static int PruneDirectory(string directory, DateTimeOffset cutoff)
    {
        var deleted = 0;

        try
        {
            if (!Directory.Exists(directory))
            {
                return 0;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*.jpg"))
            {
                try
                {
                    if (IsStableFileName(Path.GetFileName(file)))
                    {
                        continue;
                    }

                    if (File.GetLastWriteTimeUtc(file) >= cutoff.UtcDateTime)
                    {
                        continue;
                    }

                    File.Delete(file);
                    deleted++;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return deleted;
    }

    /// <summary>完整输入路径小写后的 SHA1 十六进制（40 字符）。</summary>
    private static string HashFor(string inputPath)
    {
        ArgumentNullException.ThrowIfNull(inputPath);

        var bytes = Encoding.UTF8.GetBytes(inputPath.ToLowerInvariant());
        return Convert.ToHexString(SHA1.HashData(bytes)).ToLowerInvariant();
    }
}
