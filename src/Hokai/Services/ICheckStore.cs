using Hokai.Models;

namespace Hokai.Services;

public interface ICheckStore
{
    Task AppendAsync(CheckResult result, CancellationToken cancellationToken = default);

    Task<double> GetUptimeAsync(
        string endpointId,
        TimeSpan window,
        CancellationToken cancellationToken = default);

    Task<CheckResult?> GetLastCheckAsync(
        string endpointId,
        CancellationToken cancellationToken = default);
}
