using Hokai.Models;

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
        return AtomicJsonFile.MutateAsync<CheckResult, bool>(
            _path,
            checks =>
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

        // The reporting window includes both boundaries but excludes future-dated records, which
        // prevents clock skew or malformed input from inflating current uptime.
        var checks = (await AtomicJsonFile.ReadAsync<CheckResult>(_path, cancellationToken))
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
        var checks = await AtomicJsonFile.ReadAsync<CheckResult>(_path, cancellationToken);
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
        await AtomicJsonFile.MutateAsync<CheckResult, bool>(
            _path,
            checks =>
            {
                // Strict comparison preserves the cutoff record. The shared mutation lock also keeps
                // cleanup from publishing a stale snapshot over a concurrent append.
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

        var checks = (await AtomicJsonFile.ReadAsync<CheckResult>(_path, cancellationToken))
            .Where(result => result.Timestamp >= cutoff && result.Timestamp <= now)
            .ToList();

        return checks
            .GroupBy(result => result.EndpointId, StringComparer.Ordinal)
            .Select(group =>
            {
                var items = group.ToList();
                return new EndpointSummary
                {
                    EndpointId = group.Key,
                    Uptime = items.Count > 0
                        ? items.Count(r => r.IsUp) * 100d / items.Count
                        : 0d,
                    LastCheck = items.MaxBy(r => r.Timestamp)
                };
            })
            .ToList();
    }
}
