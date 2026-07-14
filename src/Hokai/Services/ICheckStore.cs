using Hokai.Models;

namespace Hokai.Services;

public sealed class EndpointSummary
{
    public required string EndpointId { get; init; }
    public double Uptime { get; init; }
    public CheckResult? LastCheck { get; init; }
}

/// <summary>Persists health-check history and provides time-based queries over it.</summary>
public interface ICheckStore
{
    /// <summary>Appends a result while preserving the order of existing records.</summary>
    Task AppendAsync(CheckResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the percentage of successful checks in the inclusive UTC window ending now,
    /// or <c>0.0</c> when the window contains no matching checks.
    /// </summary>
    Task<double> GetUptimeAsync(
        string endpointId,
        TimeSpan window,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the matching result with the greatest timestamp.</summary>
    Task<CheckResult?> GetLastCheckAsync(
        string endpointId,
        CancellationToken cancellationToken = default);

    /// <summary>Removes results strictly older than the retention cutoff.</summary>
    Task RemoveOlderThanAsync(
        TimeSpan retention,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the entire check history once and returns uptime and last-check summaries
    /// for every endpoint with at least one result in the specified window.
    /// </summary>
    Task<IReadOnlyList<EndpointSummary>> GetBatchSummariesAsync(
        TimeSpan window,
        CancellationToken cancellationToken = default);
}
