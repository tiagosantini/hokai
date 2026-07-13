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
}
