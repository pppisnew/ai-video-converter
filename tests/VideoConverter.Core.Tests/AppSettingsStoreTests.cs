using VideoConverter.Core.Models;
using VideoConverter.Core.Services;
using Xunit;

namespace VideoConverter.Core.Tests;

public class AppSettingsStoreTests
{
    private static string NewTempDir() =>
        Path.Combine(Path.GetTempPath(), "vc-settings-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// 清理临时目录。只读用例（文件不存在）根本不会建目录，
    /// 直接 Delete 会抛 DirectoryNotFoundException，必须先判存在。
    /// </summary>
    private static void Cleanup(string dir)
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var dir = NewTempDir();
        try
        {
            var settings = new AppSettingsStore(dir).Load();

            Assert.Equal(AppTheme.System, settings.Theme);
            Assert.Equal(".mp4", settings.Container);
            Assert.Equal(23, settings.Crf);
            Assert.Equal(ResolutionScale.Original, settings.Scale);
            Assert.Equal(128, settings.AudioBitrateKbps);
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact]
    public void SaveThenLoad_RoundTripsEveryField()
    {
        var dir = NewTempDir();
        try
        {
            var store = new AppSettingsStore(dir);
            store.Save(new AppSettings
            {
                Theme = AppTheme.Dark,
                Container = ".mkv",
                Crf = 18,
                Scale = ResolutionScale.P720,
                AudioBitrateKbps = 192,
            });

            var loaded = store.Load();

            Assert.Equal(AppTheme.Dark, loaded.Theme);
            Assert.Equal(".mkv", loaded.Container);
            Assert.Equal(18, loaded.Crf);
            Assert.Equal(ResolutionScale.P720, loaded.Scale);
            Assert.Equal(192, loaded.AudioBitrateKbps);
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact]
    public void Load_CorruptFile_FallsBackToDefaults()
    {
        var dir = NewTempDir();
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "settings.json"), "{ this is not json");

            var settings = new AppSettingsStore(dir).Load();

            Assert.Equal(AppTheme.System, settings.Theme);
            Assert.Equal(23, settings.Crf);
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact]
    public void Load_OutOfRangeValues_AreClamped()
    {
        var dir = NewTempDir();
        try
        {
            Directory.CreateDirectory(dir);
            // 手改坏的设置：质量与码率越界、容器为空串
            File.WriteAllText(Path.Combine(dir, "settings.json"),
                """{"Theme":"Dark","Container":"","Crf":999,"Scale":"P720","AudioBitrateKbps":99999}""");

            var settings = new AppSettingsStore(dir).Load();

            Assert.Equal(AppTheme.Dark, settings.Theme);
            Assert.Equal(".mp4", settings.Container);
            Assert.Equal(51, settings.Crf);
            Assert.Equal(ResolutionScale.P720, settings.Scale);
            Assert.Equal(512, settings.AudioBitrateKbps);
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact]
    public void Save_CreatesDirectoryWhenMissing()
    {
        var dir = Path.Combine(NewTempDir(), "nested", "deeper");
        try
        {
            new AppSettingsStore(dir).Save(new AppSettings { Theme = AppTheme.Light });

            Assert.True(File.Exists(Path.Combine(dir, "settings.json")));
            Assert.Equal(AppTheme.Light, new AppSettingsStore(dir).Load().Theme);
        }
        finally
        {
            Cleanup(Path.GetDirectoryName(dir)!);
        }
    }

    [Fact]
    public void Load_UnknownEnumName_FallsBackToDefaults()
    {
        var dir = NewTempDir();
        try
        {
            Directory.CreateDirectory(dir);
            // JsonStringEnumConverter 遇到不认识的枚举名会抛 JsonException，
            // 由 Load 的 catch 收敛成默认值——设置文件坏了不该连带别的东西一起坏
            File.WriteAllText(Path.Combine(dir, "settings.json"),
                """{"Theme":"Neon","Container":".mkv","Crf":18}""");

            var settings = new AppSettingsStore(dir).Load();

            Assert.Equal(AppTheme.System, settings.Theme);
            Assert.Equal(23, settings.Crf);
        }
        finally
        {
            Cleanup(dir);
        }
    }
}
