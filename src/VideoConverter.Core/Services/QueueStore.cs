using System.Text.Json;
using System.Text.Json.Serialization;
using VideoConverter.Core.Models;

namespace VideoConverter.Core.Services;

/// <summary>
/// 读写 <c>%LOCALAPPDATA%\VideoConverter\queue.json</c>（未完成任务快照）。
/// 取舍与 <see cref="AppSettingsStore"/> 一致：读失败一律返回 null
/// （按"没有可恢复的任务"处理），写失败静默忽略——快照丢了顶多少恢复一批任务，
/// 不该让应用起不来，也不值得为它弹窗。
/// </summary>
public sealed class QueueStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    public QueueStore()
        : this(DefaultDirectory)
    {
    }

    public QueueStore(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        _filePath = Path.Combine(directory, "queue.json");
    }

    /// <summary>快照文件所在目录（可被测试替换）。</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VideoConverter");

    public string FilePath => _filePath;

    public QueueSnapshot? Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            var json = File.ReadAllText(_filePath);
            var snapshot = JsonSerializer.Deserialize<QueueSnapshot>(json, Options);

            // "Items": null 属于被改坏的文件；正常写入永远是数组
            return snapshot?.Items is null ? null : snapshot;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // 快照损坏 / 不可读：当作"没有可恢复的任务"，不要连锁失败
            return null;
        }
    }

    public void Save(QueueSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, JsonSerializer.Serialize(snapshot, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 磁盘满 / 只读 / 被占用：丢一次快照无所谓，不能因此崩掉
        }
    }
}
