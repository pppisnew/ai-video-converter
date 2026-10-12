using System.Text.Json;
using System.Text.Json.Serialization;
using VideoConverter.Core.Models;

namespace VideoConverter.Core.Services;

/// <summary>
/// 读写 <c>%LOCALAPPDATA%\VideoConverter\settings.json</c>。
/// 读失败（不存在 / 损坏 / 越权）时回退默认值，写失败时静默忽略——
/// 设置文件的问题绝不应该阻止应用启动，也不值得为它弹窗。
/// </summary>
public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    public AppSettingsStore()
        : this(DefaultDirectory)
    {
    }

    public AppSettingsStore(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        _filePath = Path.Combine(directory, "settings.json");
    }

    /// <summary>设置文件所在目录（可被测试替换）。</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VideoConverter");

    public string FilePath => _filePath;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_filePath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options);
            return (loaded ?? new AppSettings()).Sanitized();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // 设置文件损坏或不可读：用默认值继续，不要连锁失败
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, JsonSerializer.Serialize(settings.Sanitized(), Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 磁盘满 / 只读 / 被占用：偏好丢一次无所谓，不能因此崩掉
        }
    }
}
