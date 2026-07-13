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
    private static readonly TimeSpan ReloadInterval = TimeSpan.FromSeconds(30);
    private readonly Dictionary<string, EndpointWorker> _workers = new(StringComparer.Ordinal);

    internal async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<EndpointConfig> endpoints;
        try
        {
            endpoints = await endpointStore.GetAllAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to reload endpoint configuration.");
            return;
        }

        if (endpoints.GroupBy(endpoint => endpoint.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            logger.LogError("Endpoint reload rejected because it contains duplicate identifiers.");
            return;
        }

        var snapshot = endpoints.ToDictionary(endpoint => endpoint.Id, StringComparer.Ordinal);
        var workersToStop = _workers.Values
            .Where(worker => !snapshot.TryGetValue(worker.Endpoint.Id, out var endpoint)
                || HasMonitoringChanges(worker.Endpoint, endpoint))
            .Select(worker => worker.Endpoint.Id)
            .ToList();

        foreach (var endpointId in workersToStop)
        {
            await StopWorkerAsync(endpointId);
        }

        foreach (var endpoint in snapshot.Values.Where(endpoint => !_workers.ContainsKey(endpoint.Id)))
        {
            StartWorker(endpoint, cancellationToken);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ReloadAsync(stoppingToken);
            await RunReloadLoopAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        finally
        {
            await StopAllWorkersAsync();
        }
    }

    private async Task RunReloadLoopAsync(CancellationToken cancellationToken)
    {
        await using var timer = timerFactory.Create(ReloadInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await ReloadAsync(cancellationToken);
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

    private async Task StopWorkerAsync(string endpointId)
    {
        var worker = _workers[endpointId];
        worker.Cancellation.Cancel();
        await worker.Task;
        worker.Cancellation.Dispose();
        _workers.Remove(endpointId);
    }

    private static bool HasMonitoringChanges(EndpointConfig current, EndpointConfig updated) =>
        current.Url != updated.Url
        || current.Interval != updated.Interval
        || current.Timeout != updated.Timeout
        || !string.Equals(current.Method, updated.Method, StringComparison.Ordinal)
        || current.ExpectedStatus != updated.ExpectedStatus;

    private sealed record EndpointWorker(
        EndpointConfig Endpoint,
        CancellationTokenSource Cancellation,
        Task Task);
}
