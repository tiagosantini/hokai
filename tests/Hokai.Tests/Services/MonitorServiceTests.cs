using System.Threading.Channels;
using Hokai.Models;
using Hokai.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hokai.Tests.Services;

public sealed class MonitorServiceTests
{
    [Fact]
    public async Task PeriodicTimerFactory_CanceledWait_PropagatesCancellation()
    {
        await using var timer = new PeriodicTimerFactory().Create(TimeSpan.FromHours(1));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await timer.WaitForNextTickAsync(cancellation.Token));
    }

    [Fact]
    public async Task StartAsync_ExistingEndpoints_ChecksImmediatelyAndUsesIntervals()
    {
        var health = new RecordingHealthCheckService();
        var timers = new ControlledTimerFactory();
        var service = CreateService(
            [CreateEndpoint("one", TimeSpan.FromMinutes(1)), CreateEndpoint("two", TimeSpan.FromMinutes(5))],
            health,
            timers);

        await service.StartAsync(CancellationToken.None);
        var first = await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        var second = await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(["one", "two"], new[] { first, second }.Order());
        Assert.Equal([TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)], timers.Periods.Order());
    }

    [Fact]
    public async Task EndpointTimer_Tick_RunsNextCheckSequentially()
    {
        var health = new RecordingHealthCheckService();
        var timers = new ControlledTimerFactory();
        var service = CreateService([CreateEndpoint("one", TimeSpan.FromMinutes(1))], health, timers);
        await service.StartAsync(CancellationToken.None);
        await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));

        timers.Timers.Single().Tick();
        var endpointId = await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        await service.StopAsync(CancellationToken.None);

        Assert.Equal("one", endpointId);
        Assert.Equal(1, health.MaximumConcurrency);
    }

    [Fact]
    public async Task StopAsync_InFlightCheck_CancelsWorkerWithoutAppending()
    {
        var health = new BlockingHealthCheckService();
        var store = new RecordingCheckStore();
        var service = CreateService(
            [CreateEndpoint("one", TimeSpan.FromMinutes(1))],
            health,
            new ControlledTimerFactory(),
            store);
        await service.StartAsync(CancellationToken.None);
        await health.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await service.StopAsync(CancellationToken.None);

        Assert.Empty(store.Results);
    }

    [Fact]
    public async Task StopAsync_Workers_DisposesEndpointTimers()
    {
        var timers = new ControlledTimerFactory();
        var health = new RecordingHealthCheckService();
        var service = CreateService(
            [CreateEndpoint("one", TimeSpan.FromMinutes(1))],
            health,
            timers);
        await service.StartAsync(CancellationToken.None);
        await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));

        await service.StopAsync(CancellationToken.None);

        Assert.True(timers.Timers.Single().IsDisposed);
    }

    private static MonitorService CreateService(
        IReadOnlyList<EndpointConfig> endpoints,
        IHealthCheckService health,
        IPeriodicTimerFactory timers,
        RecordingCheckStore? store = null) => new(
            new StubEndpointStore(endpoints),
            store ?? new RecordingCheckStore(),
            health,
            new StubNotificationService(),
            timers,
            NullLoggerFactory.Instance,
            NullLogger<MonitorService>.Instance);

    private static EndpointConfig CreateEndpoint(string id, TimeSpan interval) => new()
    {
        Id = id,
        Url = new Uri($"https://example.com/{id}"),
        Interval = interval,
        Timeout = TimeSpan.FromSeconds(30),
        Method = "GET",
        ExpectedStatus = 200,
        CreatedAt = DateTimeOffset.UnixEpoch
    };

    private sealed class ControlledTimerFactory : IPeriodicTimerFactory
    {
        public List<TimeSpan> Periods { get; } = [];
        public List<ControlledTimer> Timers { get; } = [];

        public IAsyncPeriodicTimer Create(TimeSpan period)
        {
            Periods.Add(period);
            var timer = new ControlledTimer();
            Timers.Add(timer);
            return timer;
        }
    }

    private sealed class ControlledTimer : IAsyncPeriodicTimer
    {
        private readonly Channel<bool> _ticks = Channel.CreateUnbounded<bool>();
        public bool IsDisposed { get; private set; }

        public void Tick() => _ticks.Writer.TryWrite(true);

        public ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken) =>
            _ticks.Reader.ReadAsync(cancellationToken);

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            _ticks.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubEndpointStore(IReadOnlyList<EndpointConfig> endpoints) : IEndpointStore
    {
        public Task<IReadOnlyList<EndpointConfig>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(endpoints);
        public Task<EndpointConfig?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(EndpointConfig config, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingHealthCheckService : IHealthCheckService
    {
        private int _concurrency;
        public Channel<string> Calls { get; } = Channel.CreateUnbounded<string>();
        public int MaximumConcurrency { get; private set; }

        public Task<CheckResult> CheckAsync(EndpointConfig endpoint, CancellationToken cancellationToken = default)
        {
            var concurrency = Interlocked.Increment(ref _concurrency);
            MaximumConcurrency = Math.Max(MaximumConcurrency, concurrency);
            Calls.Writer.TryWrite(endpoint.Id);
            Interlocked.Decrement(ref _concurrency);
            return Task.FromResult(CreateResult(endpoint.Id));
        }
    }

    private sealed class BlockingHealthCheckService : IHealthCheckService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<CheckResult> CheckAsync(EndpointConfig endpoint, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return CreateResult(endpoint.Id);
        }
    }

    private sealed class RecordingCheckStore : ICheckStore
    {
        public List<CheckResult> Results { get; } = [];
        public Task AppendAsync(CheckResult result, CancellationToken cancellationToken = default) { Results.Add(result); return Task.CompletedTask; }
        public Task<double> GetUptimeAsync(string endpointId, TimeSpan window, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CheckResult?> GetLastCheckAsync(string endpointId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RemoveOlderThanAsync(TimeSpan retention, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubNotificationService : INotificationService
    {
        public Task NotifyDownAsync(EndpointConfig endpoint, CheckResult result, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyRecoveryAsync(EndpointConfig endpoint, CheckResult result, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static CheckResult CreateResult(string endpointId) => new()
    {
        EndpointId = endpointId,
        Timestamp = DateTimeOffset.UnixEpoch,
        IsUp = true,
        StatusCode = 200,
        ResponseTimeMs = 1
    };
}
