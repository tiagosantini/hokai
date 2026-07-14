using Hokai.Commands;
using Hokai.Models;
using Hokai.Services;
using Hokai.Tests.Support;
using System.CommandLine;

namespace Hokai.Tests.Commands;

[Collection(nameof(CommandTestHarness))]
public sealed class EndpointCommandsTests
{
    [Fact]
    public async Task HandleAddAsync_DirectCall_WritesToConsole()
    {
        var store = new FakeEndpointStore();
        var output = new StringWriter();
        var error = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            var ec = await EndpointCommands.HandleAddAsync(
                store, "https://example.com/health",
                "5m", "30s", "GET", 200, CancellationToken.None);
            Assert.Equal(0, ec);
            Assert.Contains("added", output.ToString());
            Assert.Single(store.Endpoints);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task AddCommand_ValidArgs_AddsEndpointAndExitsZero()
    {
        var store = new FakeEndpointStore();
        var checkStore = new FakeCheckStore();
        var command = EndpointCommands.Create(store, checkStore);
        var (exitCode, output, error) = await CommandTestHarness.InvokeAsync(command, "add https://example.com/health");

        Assert.Equal(0, exitCode);
        Assert.Contains("added", output);
        Assert.Single(store.Endpoints);
        Assert.Equal(new Uri("https://example.com/health"), store.Endpoints[0].Url);
        Assert.Equal(TimeSpan.FromMinutes(5), store.Endpoints[0].Interval);
        Assert.Equal(TimeSpan.FromSeconds(30), store.Endpoints[0].Timeout);
        Assert.Equal("GET", store.Endpoints[0].Method);
        Assert.Equal(200, store.Endpoints[0].ExpectedStatus);
    }

    [Fact]
    public async Task AddCommand_CustomOptions_UsesProvidedValues()
    {
        var store = new FakeEndpointStore();
        var command = EndpointCommands.Create(store, new FakeCheckStore());
        var (exitCode, _, _) = await CommandTestHarness.InvokeAsync(command, "add https://example.com --interval 00:00:10 --timeout 00:00:05 --method POST --expect 201");

        Assert.Equal(0, exitCode);
        Assert.Equal(TimeSpan.FromSeconds(10), store.Endpoints[0].Interval);
        Assert.Equal(TimeSpan.FromSeconds(5), store.Endpoints[0].Timeout);
        Assert.Equal("POST", store.Endpoints[0].Method);
        Assert.Equal(201, store.Endpoints[0].ExpectedStatus);
    }

    [Fact]
    public async Task AddCommand_IdIsGenerated()
    {
        var store = new FakeEndpointStore();
        var command = EndpointCommands.Create(store, new FakeCheckStore());
        var (exitCode, _, _) = await CommandTestHarness.InvokeAsync(command, "add https://example.com");

        Assert.Equal(0, exitCode);
        Assert.NotEmpty(store.Endpoints[0].Id);
        Assert.True(store.Endpoints[0].Id.Length >= 8);
    }

    [Fact]
    public async Task AddCommand_DuplicateId_ReturnsErrorAndWritesToErrorOutput()
    {
        var store = new FakeEndpointStore(throwOnAdd: true);
        var command = EndpointCommands.Create(store, new FakeCheckStore());
        var (exitCode, _, error) = await CommandTestHarness.InvokeAsync(command, "add https://example.com");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("already exists", error);
    }

    [Fact]
    public async Task AddCommand_NonHttpUrl_ReturnsError()
    {
        var store = new FakeEndpointStore();
        var command = EndpointCommands.Create(store, new FakeCheckStore());
        var (exitCode, _, error) = await CommandTestHarness.InvokeAsync(command, "add ftp://example.com");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("HTTP", error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(store.Endpoints);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("OPTIONS")]
    [InlineData("TRACE")]
    public async Task AddCommand_ValidMethod_AcceptsEachAllowedMethod(string method)
    {
        var store = new FakeEndpointStore();
        var command = EndpointCommands.Create(store, new FakeCheckStore());
        var (exitCode, _, _) = await CommandTestHarness.InvokeAsync(command, $"add https://example.com --method {method}");

        Assert.Equal(0, exitCode);
        Assert.Equal(method, store.Endpoints[0].Method);
    }

    [Fact]
    public async Task AddCommand_InvalidMethod_ReturnsError()
    {
        var store = new FakeEndpointStore();
        var command = EndpointCommands.Create(store, new FakeCheckStore());
        var (exitCode, _, error) = await CommandTestHarness.InvokeAsync(command, "add https://example.com --method BOGUS");

        Assert.NotEqual(0, exitCode);
        Assert.NotEmpty(error);
        Assert.Empty(store.Endpoints);
    }

    [Fact]
    public async Task AddCommand_NonPositiveInterval_ReturnsError()
    {
        var store = new FakeEndpointStore();
        var command = EndpointCommands.Create(store, new FakeCheckStore());
        var (exitCode, _, error) = await CommandTestHarness.InvokeAsync(command, "add https://example.com --interval 00:00:00");

        Assert.NotEqual(0, exitCode);
        Assert.NotEmpty(error);
        Assert.Empty(store.Endpoints);
    }

    [Fact]
    public async Task AddCommand_NonPositiveTimeout_ReturnsError()
    {
        var store = new FakeEndpointStore();
        var command = EndpointCommands.Create(store, new FakeCheckStore());
        var (exitCode, _, error) = await CommandTestHarness.InvokeAsync(command, "add https://example.com --timeout 00:00:00");

        Assert.NotEqual(0, exitCode);
        Assert.NotEmpty(error);
        Assert.Empty(store.Endpoints);
    }

    [Fact]
    public async Task AddCommand_InvalidExpectedStatus_ReturnsError()
    {
        var store = new FakeEndpointStore();
        var command = EndpointCommands.Create(store, new FakeCheckStore());
        var (exitCode, _, error) = await CommandTestHarness.InvokeAsync(command, "add https://example.com --expect 99");

        Assert.NotEqual(0, exitCode);
        Assert.NotEmpty(error);
        Assert.Empty(store.Endpoints);
    }

    [Fact]
    public async Task ListCommand_NoEndpoints_PrintsEmptyMessage()
    {
        var store = new FakeEndpointStore();
        var command = EndpointCommands.Create(store, new FakeCheckStore());
        var (exitCode, output, _) = await CommandTestHarness.InvokeAsync(command, "list");

        Assert.Equal(0, exitCode);
        Assert.Contains("No endpoints", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListCommand_WithEndpoints_PrintsTable()
    {
        var store = new FakeEndpointStore();
        store.AddEndpoint(CreateEndpoint("abc12345", "https://api.example.com/health"));
        store.AddEndpoint(CreateEndpoint("def67890", "https://blog.example.com"));
        var checkStore = new FakeCheckStore();
        checkStore.SetUptime("abc12345", 99.5);
        checkStore.SetUptime("def67890", 100.0);
        var command = EndpointCommands.Create(store, checkStore);
        var (exitCode, output, _) = await CommandTestHarness.InvokeAsync(command, "list");

        Assert.Equal(0, exitCode);
        Assert.Contains("abc12345", output);
        Assert.Contains("def67890", output);
        Assert.Contains("99.5", output);
        Assert.Contains("100.0", output);
        Assert.Contains("https://api.example.com/health", output);
    }

    [Fact]
    public async Task ListCommand_EndpointWithoutChecks_ShowsZeroUptime()
    {
        var store = new FakeEndpointStore();
        store.AddEndpoint(CreateEndpoint("abc12345", "https://example.com"));
        var command = EndpointCommands.Create(store, new FakeCheckStore());
        var (exitCode, output, _) = await CommandTestHarness.InvokeAsync(command, "list");

        Assert.Equal(0, exitCode);
        Assert.Contains("0", output);
    }

    [Fact]
    public async Task ListCommand_WithLongUri_TruncatesAndAlignsColumns()
    {
        var store = new FakeEndpointStore();
        store.AddEndpoint(CreateEndpoint("abc12345",
            "https://verybigendpoint-withwaytoomanywordsinit.com/health"));
        var command = EndpointCommands.Create(store, new FakeCheckStore());
        var (exitCode, output, _) = await CommandTestHarness.InvokeAsync(command, "list");

        Assert.Equal(0, exitCode);
        Assert.Contains("abc12345", output);
        Assert.Contains("...", output);
        Assert.DoesNotContain("waytoomanywordsinit", output);
    }

    [Fact]
    public async Task ListCommand_WithShortUri_RemainsUnchanged()
    {
        var store = new FakeEndpointStore();
        store.AddEndpoint(CreateEndpoint("abc12345", "https://example.com/health"));
        var command = EndpointCommands.Create(store, new FakeCheckStore());
        var (exitCode, output, _) = await CommandTestHarness.InvokeAsync(command, "list");

        Assert.Equal(0, exitCode);
        Assert.Contains("https://example.com/health", output);
    }

    [Fact]
    public async Task RemoveCommand_ExistingId_RemovesAndPrintsConfirmation()
    {
        var store = new FakeEndpointStore();
        store.AddEndpoint(CreateEndpoint("abc12345", "https://example.com"));
        var command = EndpointCommands.Create(store, new FakeCheckStore());
        var (exitCode, output, _) = await CommandTestHarness.InvokeAsync(command, "remove abc12345");

        Assert.Equal(0, exitCode);
        Assert.Contains("removed", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("abc12345", output);
        Assert.Empty(store.Endpoints);
    }

    [Fact]
    public async Task RemoveCommand_UnknownId_PrintsNotFound()
    {
        var store = new FakeEndpointStore();
        var command = EndpointCommands.Create(store, new FakeCheckStore());
        var (exitCode, _, error) = await CommandTestHarness.InvokeAsync(command, "remove nonexistent");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("not found", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemoveCommand_MissingId_ReturnsError()
    {
        var store = new FakeEndpointStore();
        var command = EndpointCommands.Create(store, new FakeCheckStore());
        var (exitCode, _, error) = await CommandTestHarness.InvokeAsync(command, "remove");

        Assert.NotEqual(0, exitCode);
        Assert.NotEmpty(error);
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
        private readonly bool _throwOnAdd;

        public FakeEndpointStore(bool throwOnAdd = false) => _throwOnAdd = throwOnAdd;

        public IReadOnlyList<EndpointConfig> Endpoints => _endpoints;

        public void AddEndpoint(EndpointConfig config) => _endpoints.Add(config);

        public Task<IReadOnlyList<EndpointConfig>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EndpointConfig>>(_endpoints.ToList());

        public Task<EndpointConfig?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_endpoints.FirstOrDefault(e => e.Id == id));

        public Task AddAsync(EndpointConfig config, CancellationToken cancellationToken = default)
        {
            if (_throwOnAdd)
                throw new InvalidOperationException($"Endpoint '{config.Id}' already exists.");

            _endpoints.Add(config);
            return Task.CompletedTask;
        }

        public Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default)
        {
            var removed = _endpoints.RemoveAll(e => e.Id == id) > 0;
            return Task.FromResult(removed);
        }
    }

    private sealed class FakeCheckStore : ICheckStore
    {
        private readonly Dictionary<string, double> _uptimes = new();
        private readonly Dictionary<string, CheckResult> _lastChecks = new();

        public void SetUptime(string endpointId, double percentage) => _uptimes[endpointId] = percentage;
        public void SetLastCheck(CheckResult result) => _lastChecks[result.EndpointId] = result;

        public Task AppendAsync(CheckResult result, CancellationToken cancellationToken = default)
        {
            _lastChecks[result.EndpointId] = result;
            return Task.CompletedTask;
        }

        public Task<double> GetUptimeAsync(string endpointId, TimeSpan window, CancellationToken cancellationToken = default) =>
            Task.FromResult(_uptimes.TryGetValue(endpointId, out var uptime) ? uptime : 0d);

        public Task<CheckResult?> GetLastCheckAsync(string endpointId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_lastChecks.TryGetValue(endpointId, out var result) ? result : null);

        public Task RemoveOlderThanAsync(TimeSpan retention, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
