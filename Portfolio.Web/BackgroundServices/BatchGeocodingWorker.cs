using Portfolio.Common.Models;
using Portfolio.Services.Abstractions;
using Portfolio.Services.Interfaces;

namespace Portfolio.Web.BackgroundServices;

/// <summary>
/// Drains the bounded <see cref="IBatchJobQueue"/> with a fixed number of concurrent workers, so at
/// most <see cref="WorkerCount"/> batch geocoding jobs run at once (decision D-10).
/// </summary>
public sealed class BatchGeocodingWorker : BackgroundService
{
    /// <summary>Number of jobs processed concurrently.</summary>
    public const int WorkerCount = 2;

    private readonly IBatchJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BatchGeocodingWorker> _logger;

    /// <summary>Initializes a new instance of <see cref="BatchGeocodingWorker"/>.</summary>
    public BatchGeocodingWorker(
        IBatchJobQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<BatchGeocodingWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(Enumerable.Range(0, WorkerCount).Select(_ => RunAsync(stoppingToken)));

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var item in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var service = scope.ServiceProvider.GetRequiredService<IBatchGeocodingService>();
                    await service.ProcessJobAsync(item, stoppingToken);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException && stoppingToken.IsCancellationRequested))
                {
                    // ProcessJobAsync records its own failures; this keeps the worker loop alive
                    // if the final store write itself fails, or if a cancellation that was not the
                    // host stopping escapes (it would otherwise stop the host).
                    _logger.LogError(ex, "Batch job {JobId} could not be finalized.", item.Job.JobId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown.
        }

        await FailQueuedJobsAsync();
    }

    // Best-effort on shutdown: jobs still queued will never run, so mark them Failed rather than
    // leaving them Queued in the store until its TTL.
    private async Task FailQueuedJobsAsync()
    {
        while (_queue.TryDequeue(out var item) && item is not null)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<IBatchJobStore>();
                item.Job.Status = BatchJobStatus.Failed;
                item.Job.CompletedAt = DateTimeOffset.UtcNow;
                await store.UpdateAsync(item.Job, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Queued batch job {JobId} could not be marked Failed on shutdown.", item.Job.JobId);
            }
        }
    }
}
