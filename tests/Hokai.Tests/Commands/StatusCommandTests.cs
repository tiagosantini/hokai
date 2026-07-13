using Hokai.Commands;
using Hokai.Models;
using Hokai.Services;
using Hokai.Tests.Support;
using System.CommandLine;

namespace Hokai.Tests.Commands;

[Collection(nameof(CommandTestHarness))]
public sealed class StatusCommandTests
{
    [Fact]
    public async Task StatusCommand_NoEndpoints_PrintsEmptyMessage()
    {
        var store = new FakeEndpointStore();
        var checkStore = new FakeCheckStore();
        var command = StatusCommand.Create(store, checkStore);
        var (exitCode, output, _) = await CommandTestHarness.InvokeAsync(command, "");

        Assert.Equal(0, exitCode);
        Assert.Contains("No endpoints", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StatusCommand_EndpointWithoutChecks_ShowsNoData()
    {
        var store = new FakeEndpointStore();
        store.AddEndpoint(CreateEndpoint("abc12345", "https://example.com"));
        var checkStore = new FakeCheckStore();
        var command = StatusCommand.Create(store, checkStore);
        var (exitCode, output, _) = await CommandTestHarness.InvokeAsync(command, "");

        Assert.Equal(0, exitCode);
        Assert.Contains("abc12345", output);
        Assert.Contains("—", output);
    }

    [Fact]
    public async Task StatusCommand_UpEndpoint_ShowsStatusAndUptime()
    {
        var store = new FakeEndpointStore();
        store.AddEndpoint(CreateEndpoint("abc12345", "https://example.com"));
        var checkStore = new FakeCheckStore();
        checkStore.SetLastCheck(new CheckResult
        {
            EndpointId = "abc12345",
            Timestamp = new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero),
            IsUp = true,
            StatusCode = 200,
            ResponseTimeMs = 145
        });
        checkStore.SetUptime("abc12345", 99.5);
        var command = StatusCommand.Create(store, checkStore);
        var (exitCode, output, _) = await CommandTestHarness.InvokeAsync(command, "");

        Assert.Equal(0, exitCode);
        Assert.Contains("abc12345", output);
        Assert.Contains("UP", output);
        Assert.Contains("200", output);
        Assert.Contains("145", output);
        Assert.Contains("99.5", output);
    }

    [Fact]
    public async Task StatusCommand_DownEndpoint_ShowsErrorAndNullStatus()
    {
        var store = new FakeEndpointStore();
        store.AddEndpoint(CreateEndpoint("abc12345", "https://example.com"));
        var checkStore = new FakeCheckStore();
        checkStore.SetLastCheck(new CheckResult
        {
            EndpointId = "abc12345",
            Timestamp = new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero),
            IsUp = false,
            StatusCode = null,
            ResponseTimeMs = 2000,
            Error = "Connection refused"
        });
        var command = StatusCommand.Create(store, checkStore);
        var (exitCode, output, _) = await CommandTestHarness.InvokeAsync(command, "");

        Assert.Equal(0, exitCode);
        Assert.Contains("DOWN", output);
        Assert.Contains("—", output);
    }

    [Fact]
    public async Task StatusCommand_MultipleEndpoints_PrintsAll()
    {
        var store = new FakeEndpointStore();
        store.AddEndpoint(CreateEndpoint("aaa", "https://aaa.com"));
        store.AddEndpoint(CreateEndpoint("bbb", "https://bbb.com"));
        var checkStore = new FakeCheckStore();
        var command = StatusCommand.Create(store, checkStore);
        var (exitCode, output, _) = await CommandTestHarness.InvokeAsync(command, "");

        Assert.Equal(0, exitCode);
        Assert.Contains("aaa", output);
        Assert.Contains("bbb", output);
    }

    private static EndpointConfig CreateEndpoint(string id, string url) => new()
    {
        Id = id,
        Url = new Uri(url),
        Interval = TimeSpan.FromMinutes(5),
        Timeout = TimeSpan.FromSeconds(30),
        Method = "GET",
        ExpectedStatus = 200,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private sealed class FakeEndpointStore : IEndpointStore
    {
        private readonly List<EndpointConfig> _endpoints = [];

        public IReadOnlyList<EndpointConfig> Endpoints => _endpoints;

        public void AddEndpoint(EndpointConfig config) => _endpoints.Add(config);

        public Task<IReadOnlyList<EndpointConfig>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EndpointConfig>>(_endpoints.ToList());

        public Task<EndpointConfig?> GetByIdAsync(string id, CancellationToken ct = default) =>
            Task.FromResult(_endpoints.FirstOrDefault(e => e.Id == id));

        public Task AddAsync(EndpointConfig config, CancellationToken ct = default)
        {
            _endpoints.Add(config);
            return Task.CompletedTask;
        }

        public Task<bool> RemoveAsync(string id, CancellationToken ct = default) =>
            Task.FromResult(_endpoints.RemoveAll(e => e.Id == id) > 0);
    }

    private sealed class FakeCheckStore : ICheckStore
    {
        private readonly Dictionary<string, double> _uptimes = new();
        private readonly Dictionary<string, CheckResult> _lastChecks = new();

        public void SetUptime(string endpointId, double percentage) => _uptimes[endpointId] = percentage;
        public void SetLastCheck(CheckResult result) => _lastChecks[result.EndpointId] = result;

        public Task AppendAsync(CheckResult result, CancellationToken ct = default)
        {
            _lastChecks[result.EndpointId] = result;
            return Task.CompletedTask;
        }

        public Task<double> GetUptimeAsync(string endpointId, TimeSpan window, CancellationToken ct = default) =>
            Task.FromResult(_uptimes.TryGetValue(endpointId, out var uptime) ? uptime : 0d);

        public Task<CheckResult?> GetLastCheckAsync(string endpointId, CancellationToken ct = default) =>
            Task.FromResult(_lastChecks.TryGetValue(endpointId, out var result) ? result : null);

        public Task RemoveOlderThanAsync(TimeSpan retention, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
