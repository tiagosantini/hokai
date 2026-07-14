using Hokai.Models;
using Hokai.Serialization;

namespace Hokai.Services;

public sealed class CheckStore : ICheckStore
{
    private readonly string _path;
    private readonly TimeProvider _timeProvider;

    public CheckStore(string dataDirectory, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _path = Path.Combine(dataDirectory, "checks.json");
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task AppendAsync(CheckResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return AtomicJsonFile.MutateAsync(
            _path,
            HokaiJsonContext.Default.ListCheckResult,
            (List<CheckResult> checks) =>
            {
                checks.Add(result);
                return (true, true);
            },
            cancellationToken);
    }

    public async Task<double> GetUptimeAsync(
        string endpointId,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);
        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), "Uptime window must be positive.");
        }

        var now = _timeProvider.GetUtcNow();
        var cutoff = now - window;

        var checks = (await AtomicJsonFile.ReadAsync(_path, HokaiJsonContext.Default.ListCheckResult, cancellationToken))
            .Where(result =>
                string.Equals(result.EndpointId, endpointId, StringComparison.Ordinal)
                && result.Timestamp >= cutoff
                && result.Timestamp <= now)
            .ToList();

        return checks.Count == 0
            ? 0d
            : checks.Count(result => result.IsUp) * 100d / checks.Count;
    }

    public async Task<CheckResult?> GetLastCheckAsync(
        string endpointId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);
        var checks = await AtomicJsonFile.ReadAsync(_path, HokaiJsonContext.Default.ListCheckResult, cancellationToken);
        return checks
            .Where(result => string.Equals(result.EndpointId, endpointId, StringComparison.Ordinal))
            .MaxBy(result => result.Timestamp);
    }

    public async Task RemoveOlderThanAsync(
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        if (retention < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retention), "Retention must not be negative.");
        }

        var cutoff = _timeProvider.GetUtcNow() - retention;
        await AtomicJsonFile.MutateAsync(
            _path,
            HokaiJsonContext.Default.ListCheckResult,
            (List<CheckResult> checks) =>
            {
                var removed = checks.RemoveAll(result => result.Timestamp < cutoff) > 0;
                return (removed, removed);
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<EndpointSummary>> GetBatchSummariesAsync(
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), "Summary window must be positive.");
        }

        var now = _timeProvider.GetUtcNow();
        var cutoff = now - window;

        var allChecks = (await AtomicJsonFile.ReadAsync(_path, HokaiJsonContext.Default.ListCheckResult, cancellationToken))
            .Where(result => result.Timestamp <= now)
            .ToList();

        var windowedChecks = allChecks
            .Where(result => result.Timestamp >= cutoff)
            .ToList();

        var endpointIds = allChecks
            .Select(result => result.EndpointId)
            .Distinct(StringComparer.Ordinal);

        return endpointIds.Select(id =>
        {
            var inWindow = windowedChecks
                .Where(result => string.Equals(result.EndpointId, id, StringComparison.Ordinal))
                .ToList();

            var lastCheck = allChecks
                .Where(result => string.Equals(result.EndpointId, id, StringComparison.Ordinal))
                .MaxBy(result => result.Timestamp);

            return new EndpointSummary
            {
                EndpointId = id,
                Uptime = inWindow.Count > 0
                    ? inWindow.Count(r => r.IsUp) * 100d / inWindow.Count
                    : 0d,
                LastCheck = lastCheck
            };
        }).ToList();
    }
}
