using VideoConverter.Core.Models;

namespace VideoConverter.Core.Services;

public interface IConversionQueue
{
    IReadOnlyList<QueueItem> Items { get; }

    bool IsRunning { get; }

    /// <summary>单项状态变化（状态、进度、错误）。</summary>
    event Action<QueueItem>? ItemChanged;

    /// <summary>整个队列结束（全部完成/失败或被取消）。</summary>
    event Action<QueueSummary>? QueueFinished;

    /// <summary>追加任务。输出路径冲突时自动追加 _1/_2 后缀。</summary>
    QueueItem Enqueue(string inputPath, ConversionSettings settings, MediaInfo? info = null);

    bool Remove(string itemId);

    int ClearCompleted();

    /// <summary>Select or deselect every item (header checkbox).</summary>
    void SetAllSelected(bool selected);

    bool MoveUp(string itemId);

    bool MoveDown(string itemId);

    /// <summary>Remove every item; returns how many were dropped.</summary>
    int Clear();

    int RetryFailed();

    Task RunAsync(CancellationToken cancellationToken = default);

    void CancelCurrent();

    void CancelAll();
}

public sealed record QueueSummary(int Completed, int Failed, int Cancelled, bool WasCancelled);

/// <summary>
/// 严格顺序的转换队列：同一时刻只有一个任务在转码。
/// 单个任务失败不中断后续任务；取消分"取消当前"与"取消全部"两级。
/// </summary>
public sealed class ConversionQueue : IConversionQueue
{
    private readonly IConversionService _conversionService;
    private readonly List<QueueItem> _items = new();
    private readonly SemaphoreSlim _runGate = new(1, 1);
    private CancellationTokenSource? _queueCts;
    private int _cancelledCount;

    public ConversionQueue(IConversionService conversionService)
    {
        _conversionService = conversionService;
    }

    public IReadOnlyList<QueueItem> Items => _items;

    public bool IsRunning { get; private set; }

    public event Action<QueueItem>? ItemChanged;

    public event Action<QueueSummary>? QueueFinished;

    public QueueItem Enqueue(string inputPath, ConversionSettings settings, MediaInfo? info = null)
    {
        var outputPath = ResolveOutputPath(inputPath, settings);
        var item = new QueueItem(inputPath, outputPath, settings, info);
        _items.Add(item);
        ItemChanged?.Invoke(item);
        return item;
    }

    public bool Remove(string itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item is null || item.State == QueueItemState.Converting)
        {
            return false;
        }

        _items.Remove(item);
        ItemChanged?.Invoke(item);
        return true;
    }

    /// <summary>Select or deselect every item (drives the header checkbox).</summary>
    public void SetAllSelected(bool selected)
    {
        foreach (var item in _items)
        {
            item.IsSelected = selected;
            ItemChanged?.Invoke(item);
        }
    }

    public bool MoveUp(string itemId)
    {
        var index = IndexOf(itemId);
        if (index <= 0)
        {
            return false;
        }

        (_items[index], _items[index - 1]) = (_items[index - 1], _items[index]);
        ItemChanged?.Invoke(_items[index]);
        ItemChanged?.Invoke(_items[index - 1]);
        return true;
    }

    public bool MoveDown(string itemId)
    {
        var index = IndexOf(itemId);
        if (index < 0 || index >= _items.Count - 1)
        {
            return false;
        }

        (_items[index], _items[index + 1]) = (_items[index + 1], _items[index]);
        ItemChanged?.Invoke(_items[index]);
        ItemChanged?.Invoke(_items[index + 1]);
        return true;
    }

    /// <summary>Remove every item, returning how many were dropped.</summary>
    public int Clear()
    {
        var count = _items.Count;
        _items.Clear();
        return count;
    }

    private int IndexOf(string itemId) => _items.FindIndex(i => i.Id == itemId);

    public int ClearCompleted()
    {
        var removed = _items.Where(i => i.State == QueueItemState.Completed).ToList();
        foreach (var item in removed)
        {
            _items.Remove(item);
            ItemChanged?.Invoke(item);
        }

        return removed.Count;
    }

    public int RetryFailed()
    {
        var failed = _items.Where(i => i.State == QueueItemState.Failed).ToList();
        foreach (var item in failed)
        {
            item.ResetForRetry();
            ItemChanged?.Invoke(item);
        }

        return failed.Count;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // NOTE: do NOT use ConfigureAwait(false) here. The continuation must resume on the
        // caller's synchronization context (the UI thread). Otherwise MarkCompleted/MarkFailed
        // raise PropertyChanged on a thread-pool thread and WinUI bindings silently drop it,
        // which shows up as queue items stuck on the Converting state forever.
        await _runGate.WaitAsync(cancellationToken);
        try
        {
            IsRunning = true;
            _cancelledCount = 0;
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _queueCts = linkedCts;

            var completed = 0;
            var failed = 0;

            foreach (var item in _items)
            {
                if (linkedCts.IsCancellationRequested)
                {
                    break;
                }

                // Unchecked items are skipped so the user can convert only a subset
                if (item.State != QueueItemState.Pending || !item.IsSelected)
                {
                    continue;
                }

                using var itemCts = CancellationTokenSource.CreateLinkedTokenSource(linkedCts.Token);
                item.MarkConverting(itemCts);
                ItemChanged?.Invoke(item);

                var progress = new Progress<ConversionProgress>(p =>
                {
                    item.UpdateProgress(p);
                    ItemChanged?.Invoke(item);
                });

                try
                {
                    var result = await _conversionService.ConvertAsync(
                        item.InputPath, item.OutputPath, item.Settings, progress, itemCts.Token);

                    if (result.Success)
                    {
                        item.MarkCompleted(result.UsedEncoder ?? "unknown");
                        completed++;
                    }
                    else
                    {
                        item.MarkFailed(result.Error ?? "转换失败");
                        failed++;
                    }
                }
                catch (OperationCanceledException)
                {
                    // 取消当前：只放弃这一个任务，队列继续处理后续项
                    // （对应"我不想转这个了"；"取消全部"才停摆，对应"我不想转了"）
                    item.MarkFailed("已取消", cancelled: true);
                    _cancelledCount++;
                }
                catch (Exception ex)
                {
                    item.MarkFailed(ex.Message);
                    failed++;
                }

                ItemChanged?.Invoke(item);
            }

            QueueFinished?.Invoke(new QueueSummary(completed, failed, _cancelledCount, linkedCts.IsCancellationRequested));
        }
        finally
        {
            IsRunning = false;
            _queueCts = null;
            _runGate.Release();
        }
    }

    public void CancelCurrent()
    {
        var current = _items.FirstOrDefault(i => i.State == QueueItemState.Converting);
        current?.Cancellation?.Cancel();
    }

    public void CancelAll()
    {
        _queueCts?.Cancel();
        var current = _items.FirstOrDefault(i => i.State == QueueItemState.Converting);
        current?.Cancellation?.Cancel();
    }

    /// <summary>
    /// 计算输出路径：默认与源文件同目录、加 _converted 后缀。
    /// 避开两类冲突：磁盘上已存在的文件，以及本队列其他项已占用的输出路径
    /// （后者在文件尚未生成时也会发生，例如两个队列项被指定了同一输出目录）。
    /// 冲突时追加 _1/_2… 序号，单调递增、不回收。
    /// </summary>
    internal string ResolveOutputPath(string inputPath, ConversionSettings settings)
    {
        var directory = Path.GetDirectoryName(inputPath);
        var name = Path.GetFileNameWithoutExtension(inputPath) ?? "output";
        var extension = settings.AudioOnly ? ".mp3" : settings.Container;

        string Build(int? index)
        {
            var suffix = index is null ? string.Empty : $"_{index}";
            var fileName = $"{name}_converted{suffix}{extension}";
            return string.IsNullOrEmpty(directory) ? fileName : Path.Combine(directory, fileName);
        }

        if (IsFree(Build(null)))
        {
            return Build(null);
        }

        for (var index = 1; index < 1000; index++)
        {
            var candidate = Build(index);
            if (IsFree(candidate))
            {
                return candidate;
            }
        }

        return Build(Guid.NewGuid().GetHashCode());
    }

    private bool IsFree(string candidatePath) =>
        !File.Exists(candidatePath)
        && !_items.Any(i => string.Equals(i.OutputPath, candidatePath, StringComparison.OrdinalIgnoreCase));
}
