using Hokai.Models;
using Hokai.Serialization;
using System.Runtime.InteropServices;

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

        var allChecks = await AtomicJsonFile.ReadAsync(_path, HokaiJsonContext.Default.ListCheckResult, cancellationToken);

        var groups = new Dictionary<string, (int Total, int Up, CheckResult? Last)>(StringComparer.Ordinal);

        foreach (var check in allChecks)
        {
            if (check.Timestamp > now)
                continue;

            ref var group = ref CollectionsMarshal.GetValueRefOrAddDefault(groups, check.EndpointId, out var exists);
            if (!exists)
                group = (0, 0, null);

            if (check.Timestamp >= cutoff)
            {
                group.Total++;
                if (check.IsUp)
                    group.Up++;
            }

            if (group.Last == null || check.Timestamp > group.Last.Timestamp)
                group.Last = check;
        }

        var summaries = new List<EndpointSummary>(groups.Count);
        foreach (var kvp in groups)
        {
            summaries.Add(new EndpointSummary
            {
                EndpointId = kvp.Key,
                Uptime = kvp.Value.Total > 0
                    ? kvp.Value.Up * 100d / kvp.Value.Total
                    : 0d,
                LastCheck = kvp.Value.Last
            });
        }

        return summaries;
    }
}
