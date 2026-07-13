using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Hokai.Models;

namespace Hokai.Services;

public sealed class MonitorService(
    IEndpointStore endpointStore,
    ICheckStore checkStore,
    IHealthCheckService healthCheckService,
    INotificationService notificationService,
    IPeriodicTimerFactory timerFactory,
    ILoggerFactory loggerFactory,
    ILogger<MonitorService> logger) : BackgroundService
{
    private readonly Dictionary<string, EndpointWorker> _workers = new(StringComparer.Ordinal);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IReadOnlyList<EndpointConfig> endpoints;
        try
        {
            endpoints = await endpointStore.GetAllAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to load endpoint configuration.");
            endpoints = [];
        }

        foreach (var endpoint in endpoints)
        {
            StartWorker(endpoint, stoppingToken);
        }

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await StopAllWorkersAsync();
        }
    }

    private void StartWorker(EndpointConfig endpoint, CancellationToken stoppingToken)
    {
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var session = new EndpointMonitorSession(
            healthCheckService,
            checkStore,
            notificationService,
            loggerFactory.CreateLogger<EndpointMonitorSession>());
        var task = RunWorkerAsync(endpoint, session, cancellation.Token);
        _workers.Add(endpoint.Id, new EndpointWorker(endpoint, cancellation, task));
    }

    private async Task RunWorkerAsync(
        EndpointConfig endpoint,
        EndpointMonitorSession session,
        CancellationToken cancellationToken)
    {
        await using var timer = timerFactory.Create(endpoint.Interval);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await session.CheckOnceAsync(endpoint, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Endpoint worker failed for {EndpointId}.", endpoint.Id);
                }

                if (!await timer.WaitForNextTickAsync(cancellationToken))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task StopAllWorkersAsync()
    {
        foreach (var worker in _workers.Values)
        {
            worker.Cancellation.Cancel();
        }

        await Task.WhenAll(_workers.Values.Select(worker => worker.Task));
        foreach (var worker in _workers.Values)
        {
            worker.Cancellation.Dispose();
        }

        _workers.Clear();
    }

    private sealed record EndpointWorker(
        EndpointConfig Endpoint,
        CancellationTokenSource Cancellation,
        Task Task);
}
