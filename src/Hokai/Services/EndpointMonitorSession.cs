using Hokai.Models;
using Microsoft.Extensions.Logging;

namespace Hokai.Services;

internal sealed class EndpointMonitorSession(
    IHealthCheckService healthCheckService,
    ICheckStore checkStore,
    INotificationService notificationService,
    ILogger<EndpointMonitorSession> logger)
{
    private bool? _lastState;

    public async Task CheckOnceAsync(
        EndpointConfig endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var result = await healthCheckService.CheckAsync(endpoint, cancellationToken);

        try
        {
            await checkStore.AppendAsync(result, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Persistence is the commit point; side effects and state must not outrun recorded history.
            logger.LogError(exception, "Failed to persist check for endpoint {EndpointId}.", endpoint.Id);
            return;
        }

        if (_lastState.HasValue && _lastState.Value != result.IsUp)
        {
            try
            {
                if (result.IsUp)
                {
                    await notificationService.NotifyRecoveryAsync(endpoint, result, cancellationToken);
                }
                else
                {
                    await notificationService.NotifyDownAsync(endpoint, result, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to notify transition for endpoint {EndpointId}.", endpoint.Id);
            }
        }

        _lastState = result.IsUp;
    }
}
