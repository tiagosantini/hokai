using Hokai.Models;

namespace Hokai.Services;

/// <summary>Sends endpoint state-transition notifications.</summary>
public interface INotificationService
{
    Task NotifyDownAsync(
        EndpointConfig endpoint,
        CheckResult result,
        CancellationToken cancellationToken = default);

    Task NotifyRecoveryAsync(
        EndpointConfig endpoint,
        CheckResult result,
        CancellationToken cancellationToken = default);
}
