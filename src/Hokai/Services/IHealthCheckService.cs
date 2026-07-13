using Hokai.Models;

namespace Hokai.Services;

/// <summary>Executes HTTP checks and maps observations to persisted results.</summary>
public interface IHealthCheckService
{
    Task<CheckResult> CheckAsync(
        EndpointConfig endpoint,
        CancellationToken cancellationToken = default);
}
