using System.Threading.Channels;
using Portfolio.Services.Abstractions;

namespace Portfolio.Services;

/// <summary>
/// <see cref="IBatchJobQueue"/> backed by a bounded <see cref="Channel{T}"/>, kept small by design (decision D-10)
/// because batch geocoding fans out to a paid API and a free-tier Redis.
/// </summary>
public sealed class BoundedBatchJobQueue : IBatchJobQueue
{
    /// <summary>Default number of jobs that may wait in the queue.</summary>
    public const int DefaultCapacity = 8;

    private readonly Channel<BatchWorkItem> _channel;

    /// <summary>Initializes a queue with the given <paramref name="capacity"/>.</summary>
    public BoundedBatchJobQueue(int capacity = DefaultCapacity)
    {
        Capacity = capacity;
        _channel = Channel.CreateBounded<BatchWorkItem>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = false,
            AllowSynchronousContinuations = false
        });
    }

    /// <inheritdoc/>
    public int Capacity { get; }

    /// <inheritdoc/>
    public bool TryEnqueue(BatchWorkItem item) => _channel.Writer.TryWrite(item);

    /// <inheritdoc/>
    public bool TryDequeue(out BatchWorkItem? item) => _channel.Reader.TryRead(out item);

    /// <inheritdoc/>
    public IAsyncEnumerable<BatchWorkItem> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
