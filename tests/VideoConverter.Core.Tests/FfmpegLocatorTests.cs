using VideoConverter.Core.Services;
using Xunit;

namespace VideoConverter.Core.Tests;

public class FfmpegLocatorTests
{
    [Fact]
    public void Locate_FindsBundledDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "vc-locator-" + Guid.NewGuid().ToString("N"));
        var bundled = Path.Combine(root, "tools", "ffmpeg");
        Directory.CreateDirectory(bundled);
        File.WriteAllText(Path.Combine(bundled, "ffmpeg.exe"), "stub");
        File.WriteAllText(Path.Combine(bundled, "ffprobe.exe"), "stub");

        try
        {
            var found = FfmpegLocator.ProbeDirectory(bundled, FfmpegSource.Bundled);

            Assert.NotNull(found);
            Assert.Equal(FfmpegSource.Bundled, found!.Source);
            Assert.EndsWith("ffmpeg.exe", found.FfmpegPath);
            Assert.EndsWith("ffprobe.exe", found.FfprobePath);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Locate_MissingBinaries_ReturnsNull()
    {
        var root = Path.Combine(Path.GetTempPath(), "vc-locator-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            Assert.Null(FfmpegLocator.ProbeDirectory(root, FfmpegSource.SystemPath));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void WalkUpForToolsDirectory_YieldsParentThenAncestors()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "a", "b", "c");

        var candidates = FfmpegLocator.WalkUpForToolsDirectory(baseDir).ToList();

        Assert.Contains(Path.Combine(Path.GetTempPath(), "a", "b", "c", "tools", "ffmpeg"), candidates);
        Assert.Contains(Path.Combine(Path.GetTempPath(), "a", "b", "tools", "ffmpeg"), candidates);
        Assert.Contains(Path.Combine(Path.GetTempPath(), "a", "tools", "ffmpeg"), candidates);
    }
}
