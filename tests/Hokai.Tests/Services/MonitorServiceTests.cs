using System.Threading.Channels;
using Hokai.Models;
using Hokai.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hokai.Tests.Services;

public sealed class MonitorServiceTests
{
    private static readonly TimeSpan TestWatchdog = TimeSpan.FromSeconds(10);
    public static TheoryData<EndpointConfig> ChangedMonitoringEndpoints => new()
    {
        CopyEndpoint(CreateEndpoint("one", TimeSpan.FromMinutes(1)), url: new Uri("https://changed.example.com")),
        CopyEndpoint(CreateEndpoint("one", TimeSpan.FromMinutes(1)), interval: TimeSpan.FromMinutes(2)),
        CopyEndpoint(CreateEndpoint("one", TimeSpan.FromMinutes(1)), timeout: TimeSpan.FromSeconds(10)),
        CopyEndpoint(CreateEndpoint("one", TimeSpan.FromMinutes(1)), method: "HEAD"),
        CopyEndpoint(CreateEndpoint("one", TimeSpan.FromMinutes(1)), expectedStatus: 204)
    };

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
        var first = await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TestWatchdog);
        var second = await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TestWatchdog);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(["one", "two"], new[] { first, second }.Order());
        Assert.Equal(
            [TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromHours(1)],
            timers.Periods.Order());
    }

    [Fact]
    public async Task EndpointTimer_Tick_RunsNextCheckSequentially()
    {
        var health = new RecordingHealthCheckService();
        var timers = new ControlledTimerFactory();
        var service = CreateService([CreateEndpoint("one", TimeSpan.FromMinutes(1))], health, timers);
        await service.StartAsync(CancellationToken.None);
        await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TestWatchdog);

        timers.Timers.Single(timer => timer.Period == TimeSpan.FromMinutes(1)).Tick();
        var endpointId = await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TestWatchdog);
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
        await health.Started.Task.WaitAsync(TestWatchdog);

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
        await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TestWatchdog);

        await service.StopAsync(CancellationToken.None);

        Assert.All(timers.Timers, timer => Assert.True(timer.IsDisposed));
    }

    [Fact]
    public async Task ReloadAsync_AddedEndpoint_StartsImmediateWorker()
    {
        var endpoints = new MutableEndpointStore([CreateEndpoint("one", TimeSpan.FromMinutes(1))]);
        var health = new RecordingHealthCheckService();
        var service = CreateService(endpoints, health, new ControlledTimerFactory());
        await service.StartAsync(CancellationToken.None);
        await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TestWatchdog);
        endpoints.Endpoints =
            [CreateEndpoint("one", TimeSpan.FromMinutes(1)), CreateEndpoint("two", TimeSpan.FromMinutes(2))];

        await service.ReloadAsync();
        var addedId = await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TestWatchdog);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal("two", addedId);
    }

    [Fact]
    public async Task ReloadAsync_RemovedEndpoint_CancelsAndDisposesWorker()
    {
        var endpoints = new MutableEndpointStore([CreateEndpoint("one", TimeSpan.FromMinutes(1))]);
        var health = new RecordingHealthCheckService();
        var timers = new ControlledTimerFactory();
        var service = CreateService(endpoints, health, timers);
        await service.StartAsync(CancellationToken.None);
        await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TestWatchdog);
        endpoints.Endpoints = [];

        await service.ReloadAsync();

        Assert.True(timers.Timers.Single(timer => timer.Period == TimeSpan.FromMinutes(1)).IsDisposed);
        await service.StopAsync(CancellationToken.None);
    }

    [Theory]
    [MemberData(nameof(ChangedMonitoringEndpoints))]
    public async Task ReloadAsync_ChangedMonitoringSettings_RestartsWorker(EndpointConfig updated)
    {
        var endpoints = new MutableEndpointStore([CreateEndpoint("one", TimeSpan.FromMinutes(1))]);
        var health = new RecordingHealthCheckService();
        var timers = new ControlledTimerFactory();
        var service = CreateService(endpoints, health, timers);
        await service.StartAsync(CancellationToken.None);
        await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TestWatchdog);
        endpoints.Endpoints = [updated];

        await service.ReloadAsync();
        await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TestWatchdog);

        Assert.Equal(4, timers.Timers.Count);
        Assert.True(timers.Timers[0].IsDisposed);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ReloadAsync_CreatedAtOnlyChange_DoesNotRestartWorker()
    {
        var original = CreateEndpoint("one", TimeSpan.FromMinutes(1));
        var endpoints = new MutableEndpointStore([original]);
        var health = new RecordingHealthCheckService();
        var timers = new ControlledTimerFactory();
        var service = CreateService(endpoints, health, timers);
        await service.StartAsync(CancellationToken.None);
        await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TestWatchdog);
        endpoints.Endpoints = [CopyEndpoint(original, createdAt: original.CreatedAt.AddDays(1))];

        await service.ReloadAsync();

        Assert.Equal(3, timers.Timers.Count);
        Assert.False(timers.Timers.Single(timer => timer.Period == TimeSpan.FromMinutes(1)).IsDisposed);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ReloadAsync_DuplicateIds_KeepsExistingWorker()
    {
        var endpoint = CreateEndpoint("one", TimeSpan.FromMinutes(1));
        var endpoints = new MutableEndpointStore([endpoint]);
        var health = new RecordingHealthCheckService();
        var timers = new ControlledTimerFactory();
        var service = CreateService(endpoints, health, timers);
        await service.StartAsync(CancellationToken.None);
        await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TestWatchdog);
        endpoints.Endpoints = [endpoint, endpoint];

        await service.ReloadAsync();

        Assert.Equal(3, timers.Timers.Count);
        Assert.False(timers.Timers.Single(timer => timer.Period == TimeSpan.FromMinutes(1)).IsDisposed);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ReloadAsync_NonPositiveInterval_KeepsExistingWorker()
    {
        var endpoint = CreateEndpoint("one", TimeSpan.FromMinutes(1));
        var endpoints = new MutableEndpointStore([endpoint]);
        var health = new RecordingHealthCheckService();
        var timers = new ControlledTimerFactory();
        var service = CreateService(endpoints, health, timers);
        await service.StartAsync(CancellationToken.None);
        try
        {
            await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TestWatchdog);
            var activeTimer = timers.Timers.Single(timer => timer.Period == TimeSpan.FromMinutes(1));
            endpoints.Endpoints = [CopyEndpoint(endpoint, interval: TimeSpan.Zero)];

            await service.ReloadAsync();

            Assert.False(activeTimer.IsDisposed);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ReloadAsync_ReadFailure_KeepsExistingWorker()
    {
        var endpoints = new MutableEndpointStore([CreateEndpoint("one", TimeSpan.FromMinutes(1))]);
        var health = new RecordingHealthCheckService();
        var timers = new ControlledTimerFactory();
        var service = CreateService(endpoints, health, timers);
        await service.StartAsync(CancellationToken.None);
        await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TestWatchdog);
        endpoints.Exception = new IOException("Invalid file");

        await service.ReloadAsync();

        Assert.Equal(3, timers.Timers.Count);
        Assert.False(timers.Timers.Single(timer => timer.Period == TimeSpan.FromMinutes(1)).IsDisposed);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CleanupTimer_Tick_RemovesConfiguredRetentionWithoutStartupCleanup()
    {
        var health = new RecordingHealthCheckService();
        var timers = new ControlledTimerFactory();
        var store = new RecordingCheckStore();
        var service = CreateService(
            [CreateEndpoint("one", TimeSpan.FromMinutes(1))],
            health,
            timers,
            store,
            new AppSettings { RetentionDays = 7 });
        await service.StartAsync(CancellationToken.None);
        await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TestWatchdog);
        Assert.Equal(0, store.CleanupCount);

        timers.Timers.Single(timer => timer.Period == TimeSpan.FromHours(1)).Tick();
        var retention = await store.CleanupCalls.Reader.ReadAsync().AsTask().WaitAsync(TestWatchdog);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(TimeSpan.FromDays(7), retention);
    }

    [Fact]
    public async Task CleanupAsync_Failure_DoesNotStopEndpointWorker()
    {
        var health = new RecordingHealthCheckService();
        var timers = new ControlledTimerFactory();
        var store = new RecordingCheckStore { CleanupException = new IOException("Write failed") };
        var service = CreateService(
            [CreateEndpoint("one", TimeSpan.FromMinutes(1))], health, timers, store);
        await service.StartAsync(CancellationToken.None);
        await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TestWatchdog);

        await service.CleanupAsync();
        timers.Timers.Single(timer => timer.Period == TimeSpan.FromMinutes(1)).Tick();
        var endpointId = await health.Calls.Reader.ReadAsync().AsTask().WaitAsync(TestWatchdog);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal("one", endpointId);
    }

    [Fact]
    public async Task CleanupAsync_CallerCancellation_Propagates()
    {
        var store = new RecordingCheckStore { CleanupException = new OperationCanceledException() };
        var service = CreateService(
            [], new RecordingHealthCheckService(), new ControlledTimerFactory(), store);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CleanupAsync(cancellation.Token));
    }

    [Fact]
    public void Constructor_NegativeRetention_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateService(
            [],
            new RecordingHealthCheckService(),
            new ControlledTimerFactory(),
            settings: new AppSettings { RetentionDays = -1 }));
    }

    private static MonitorService CreateService(
        IReadOnlyList<EndpointConfig> endpoints,
        IHealthCheckService health,
        IPeriodicTimerFactory timers,
        RecordingCheckStore? store = null,
        AppSettings? settings = null) => CreateService(
            new MutableEndpointStore(endpoints), health, timers, store, settings);

    private static MonitorService CreateService(
        IEndpointStore endpointStore,
        IHealthCheckService health,
        IPeriodicTimerFactory timers,
        RecordingCheckStore? store = null,
        AppSettings? settings = null) => new(
            endpointStore,
            store ?? new RecordingCheckStore(),
            health,
            new StubNotificationService(),
            timers,
            settings ?? new AppSettings(),
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

    private static EndpointConfig CopyEndpoint(
        EndpointConfig endpoint,
        Uri? url = null,
        TimeSpan? interval = null,
        TimeSpan? timeout = null,
        string? method = null,
        int? expectedStatus = null,
        DateTimeOffset? createdAt = null) => new()
    {
        Id = endpoint.Id,
        Url = url ?? endpoint.Url,
        Interval = interval ?? endpoint.Interval,
        Timeout = timeout ?? endpoint.Timeout,
        Method = method ?? endpoint.Method,
        ExpectedStatus = expectedStatus ?? endpoint.ExpectedStatus,
        CreatedAt = createdAt ?? endpoint.CreatedAt
    };

    private sealed class ControlledTimerFactory : IPeriodicTimerFactory
    {
        public List<TimeSpan> Periods { get; } = [];
        public List<ControlledTimer> Timers { get; } = [];

        public IAsyncPeriodicTimer Create(TimeSpan period)
        {
            Periods.Add(period);
            var timer = new ControlledTimer(period);
            Timers.Add(timer);
            return timer;
        }
    }

    private sealed class ControlledTimer(TimeSpan period) : IAsyncPeriodicTimer
    {
        private readonly Channel<bool> _ticks = Channel.CreateUnbounded<bool>();
        public TimeSpan Period { get; } = period;
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

    private sealed class MutableEndpointStore(IReadOnlyList<EndpointConfig> endpoints) : IEndpointStore
    {
        public IReadOnlyList<EndpointConfig> Endpoints { get; set; } = endpoints;
        public Exception? Exception { get; set; }

        public Task<IReadOnlyList<EndpointConfig>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Exception is null ? Task.FromResult(Endpoints) : Task.FromException<IReadOnlyList<EndpointConfig>>(Exception);
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
        public Channel<TimeSpan> CleanupCalls { get; } = Channel.CreateUnbounded<TimeSpan>();
        public int CleanupCount { get; private set; }
        public Exception? CleanupException { get; init; }
        public Task AppendAsync(CheckResult result, CancellationToken cancellationToken = default) { Results.Add(result); return Task.CompletedTask; }
        public Task<double> GetUptimeAsync(string endpointId, TimeSpan window, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CheckResult?> GetLastCheckAsync(string endpointId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RemoveOlderThanAsync(TimeSpan retention, CancellationToken cancellationToken = default)
        {
            CleanupCount++;
            CleanupCalls.Writer.TryWrite(retention);
            return CleanupException is null ? Task.CompletedTask : Task.FromException(CleanupException);
        }
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
