using VideoConverter.Core.Services;
using Xunit;

namespace VideoConverter.Core.Tests;

public class ThumbnailCacheTests
{
    [Fact]
    public void FileNameFor_SamePath_IsStable()
    {
        const string path = @"C:\videos\demo.mp4";

        Assert.Equal(ThumbnailCache.FileNameFor(path), ThumbnailCache.FileNameFor(path));
    }

    [Fact]
    public void FileNameFor_IsCaseInsensitive()
    {
        Assert.Equal(
            ThumbnailCache.FileNameFor(@"C:\Videos\Demo.MP4"),
            ThumbnailCache.FileNameFor(@"c:\videos\demo.mp4"));
    }

    [Fact]
    public void FileNameFor_DifferentPaths_Differ()
    {
        Assert.NotEqual(
            ThumbnailCache.FileNameFor(@"C:\videos\a.mp4"),
            ThumbnailCache.FileNameFor(@"C:\videos\b.mp4"));
    }

    [Fact]
    public void FileNameFor_IsSha1HexWithJpgSuffix()
    {
        var name = ThumbnailCache.FileNameFor(@"C:\videos\demo.mp4");

        Assert.EndsWith(".jpg", name, StringComparison.Ordinal);
        var hex = name[..^4];
        Assert.Equal(40, hex.Length);
        Assert.All(hex, c => Assert.True(Uri.IsHexDigit(c)));
    }

    [Fact]
    public void FileNameFor_ExtensionlessPath_StillProducesJpg()
    {
        // 缓存名不能靠扩展名推导：无扩展名或纯音频文件也要有稳定名字
        Assert.EndsWith(".jpg", ThumbnailCache.FileNameFor("no-extension-file"), StringComparison.Ordinal);
    }

    [Fact]
    public void PathFor_CombinesCacheDirectoryWithFileName()
    {
        const string path = @"C:\videos\demo.mp4";

        Assert.Equal(
            Path.Combine(ThumbnailCache.DirectoryPath, ThumbnailCache.FileNameFor(path)),
            ThumbnailCache.PathFor(path));
    }

    [Fact]
    public void FileNameFor_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ThumbnailCache.FileNameFor(null!));
    }

    [Fact]
    public void FileNameForRecapture_AppendsTimestampToStableHash()
    {
        const string path = @"C:\videos\demo.mp4";

        var name = ThumbnailCache.FileNameForRecapture(path, 1_760_000_000_000);
        var stable = ThumbnailCache.FileNameFor(path);

        Assert.Equal(stable[..40], name[..40]);            // 同一个 sha1 前缀：一眼能看出属于哪个文件
        Assert.Equal('_', name[40]);
        Assert.Equal("1760000000000", name[41..^4]);       // 毫秒时间戳，保证每次重新截取都是新文件
        Assert.EndsWith(".jpg", name, StringComparison.Ordinal);
    }

    [Fact]
    public void FileNameForRecapture_DifferentTicks_ProduceDifferentNames()
    {
        Assert.NotEqual(
            ThumbnailCache.FileNameForRecapture(@"C:\videos\demo.mp4", 1),
            ThumbnailCache.FileNameForRecapture(@"C:\videos\demo.mp4", 2));
    }

    [Fact]
    public void PathForRecapture_CombinesCacheDirectoryWithRecaptureFileName()
    {
        const string path = @"C:\videos\demo.mp4";

        Assert.Equal(
            Path.Combine(ThumbnailCache.DirectoryPath, ThumbnailCache.FileNameForRecapture(path, 42)),
            ThumbnailCache.PathForRecapture(path, 42));
    }

    [Fact]
    public void IsStableFileName_MatchesOnlySha1Jpg()
    {
        var stable = ThumbnailCache.FileNameFor(@"C:\videos\demo.mp4");

        Assert.True(ThumbnailCache.IsStableFileName(stable));
        Assert.True(ThumbnailCache.IsStableFileName(stable.ToUpperInvariant()));   // 磁盘不区分大小写
        Assert.False(ThumbnailCache.IsStableFileName(ThumbnailCache.FileNameForRecapture(@"C:\videos\demo.mp4", 1)));
        Assert.False(ThumbnailCache.IsStableFileName("abc.jpg"));
        Assert.False(ThumbnailCache.IsStableFileName(new string('z', 40) + ".jpg"));
        Assert.False(ThumbnailCache.IsStableFileName(null!));
    }

    [Fact]
    public void PruneDirectory_RemovesOnlyStaleRecaptureFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vc-thumbs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var stable = Path.Combine(dir, ThumbnailCache.FileNameFor(@"C:\videos\demo.mp4"));
            var staleRecapture = Path.Combine(dir, ThumbnailCache.FileNameForRecapture(@"C:\videos\demo.mp4", 1));
            var freshRecapture = Path.Combine(dir, ThumbnailCache.FileNameForRecapture(@"C:\videos\demo.mp4", 2));

            foreach (var file in new[] { stable, staleRecapture, freshRecapture })
            {
                File.WriteAllBytes(file, new byte[] { 1 });
            }

            // 超过 7 天的重新截取产物该删；它的同名稳定缓存（同样很旧）必须留着
            File.SetLastWriteTimeUtc(stable, now.AddDays(-30).UtcDateTime);
            File.SetLastWriteTimeUtc(staleRecapture, now.AddDays(-8).UtcDateTime);
            File.SetLastWriteTimeUtc(freshRecapture, now.AddDays(-1).UtcDateTime);

            var deleted = ThumbnailCache.PruneDirectory(dir, now.AddDays(-7));

            Assert.Equal(1, deleted);
            Assert.True(File.Exists(stable));
            Assert.False(File.Exists(staleRecapture));
            Assert.True(File.Exists(freshRecapture));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    [Fact]
    public void PruneDirectory_MissingDirectory_ReturnsZero()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vc-thumbs-missing-" + Guid.NewGuid().ToString("N"));

        Assert.Equal(0, ThumbnailCache.PruneDirectory(dir, DateTimeOffset.UtcNow));
    }
}
