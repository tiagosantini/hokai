using Hokai.Models;
using Hokai.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hokai.Tests.Services;

public sealed class EndpointMonitorSessionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CheckOnceAsync_FirstResult_PersistsWithoutNotification(bool isUp)
    {
        var context = CreateContext(isUp);

        await context.Session.CheckOnceAsync(CreateEndpoint());

        Assert.Single(context.Store.Results);
        Assert.Empty(context.Notifier.Notifications);
    }

    [Fact]
    public async Task CheckOnceAsync_UpThenDown_SendsDownNotificationOnce()
    {
        var context = CreateContext(true, false, false);

        await context.Session.CheckOnceAsync(CreateEndpoint());
        await context.Session.CheckOnceAsync(CreateEndpoint());
        await context.Session.CheckOnceAsync(CreateEndpoint());

        Assert.Equal(["down"], context.Notifier.Notifications);
    }

    [Fact]
    public async Task CheckOnceAsync_DownThenUp_SendsRecoveryNotificationOnce()
    {
        var context = CreateContext(false, true, true);

        await context.Session.CheckOnceAsync(CreateEndpoint());
        await context.Session.CheckOnceAsync(CreateEndpoint());
        await context.Session.CheckOnceAsync(CreateEndpoint());

        Assert.Equal(["recovery"], context.Notifier.Notifications);
    }

    [Fact]
    public async Task CheckOnceAsync_Transition_AppendsBeforeNotification()
    {
        var events = new List<string>();
        var context = CreateContext([true, false], events);

        await context.Session.CheckOnceAsync(CreateEndpoint());
        await context.Session.CheckOnceAsync(CreateEndpoint());

        Assert.Equal(["append", "append", "down"], events);
    }

    [Fact]
    public async Task CheckOnceAsync_AppendFailure_DoesNotNotifyOrAdvanceState()
    {
        var context = CreateContext(true, false);
        context.Store.FailNextAppend = true;

        await context.Session.CheckOnceAsync(CreateEndpoint());
        await context.Session.CheckOnceAsync(CreateEndpoint());

        Assert.Empty(context.Notifier.Notifications);
        Assert.Single(context.Store.Results);
    }

    [Fact]
    public async Task CheckOnceAsync_NotificationFailure_AdvancesStateWithoutRepeatingTransition()
    {
        var context = CreateContext(true, false, false);
        context.Notifier.Exception = new InvalidOperationException("SMTP unavailable");

        await context.Session.CheckOnceAsync(CreateEndpoint());
        await context.Session.CheckOnceAsync(CreateEndpoint());
        await context.Session.CheckOnceAsync(CreateEndpoint());

        Assert.Equal(["down"], context.Notifier.Notifications);
        Assert.Equal(3, context.Store.Results.Count);
    }

    [Fact]
    public async Task CheckOnceAsync_CanceledHealthCheck_DoesNotAppendOrNotify()
    {
        var context = CreateContext(true);
        context.Health.Exception = new OperationCanceledException();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            context.Session.CheckOnceAsync(CreateEndpoint(), new CancellationToken(canceled: true)));

        Assert.Empty(context.Store.Results);
        Assert.Empty(context.Notifier.Notifications);
    }

    private static TestContext CreateContext(params bool[] states) => CreateContext(states, []);

    private static TestContext CreateContext(bool[] states, List<string> events)
    {
        var health = new QueueHealthCheckService(states.Select(CreateResult));
        var store = new RecordingCheckStore(events);
        var notifier = new RecordingNotificationService(events);
        var session = new EndpointMonitorSession(
            health, store, notifier, NullLogger<EndpointMonitorSession>.Instance);
        return new TestContext(session, health, store, notifier);
    }

    private static EndpointConfig CreateEndpoint() => new()
    {
        Id = "endpoint",
        Url = new Uri("https://example.com/health"),
        Interval = TimeSpan.FromMinutes(1),
        Timeout = TimeSpan.FromSeconds(30),
        Method = "GET",
        ExpectedStatus = 200,
        CreatedAt = DateTimeOffset.UnixEpoch
    };

    private static CheckResult CreateResult(bool isUp) => new()
    {
        EndpointId = "endpoint",
        Timestamp = DateTimeOffset.UnixEpoch,
        IsUp = isUp,
        StatusCode = isUp ? 200 : 503,
        ResponseTimeMs = 10
    };

    private sealed record TestContext(
        EndpointMonitorSession Session,
        QueueHealthCheckService Health,
        RecordingCheckStore Store,
        RecordingNotificationService Notifier);

    private sealed class QueueHealthCheckService(IEnumerable<CheckResult> results) : IHealthCheckService
    {
        private readonly Queue<CheckResult> _results = new(results);
        public Exception? Exception { get; set; }

        public Task<CheckResult> CheckAsync(
            EndpointConfig endpoint,
            CancellationToken cancellationToken = default) =>
            Exception is null
                ? Task.FromResult(_results.Dequeue())
                : Task.FromException<CheckResult>(Exception);
    }

    private sealed class RecordingCheckStore(List<string> events) : ICheckStore
    {
        public List<CheckResult> Results { get; } = [];
        public bool FailNextAppend { get; set; }

        public Task AppendAsync(CheckResult result, CancellationToken cancellationToken = default)
        {
            if (FailNextAppend)
            {
                FailNextAppend = false;
                return Task.FromException(new IOException("Write failed"));
            }

            Results.Add(result);
            events.Add("append");
            return Task.CompletedTask;
        }

        public Task<double> GetUptimeAsync(string endpointId, TimeSpan window, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CheckResult?> GetLastCheckAsync(string endpointId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RemoveOlderThanAsync(TimeSpan retention, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<EndpointSummary>> GetBatchSummariesAsync(TimeSpan window, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingNotificationService(List<string> events) : INotificationService
    {
        public List<string> Notifications { get; } = [];
        public Exception? Exception { get; set; }

        public Task NotifyDownAsync(EndpointConfig endpoint, CheckResult result, CancellationToken cancellationToken = default) => Record("down");
        public Task NotifyRecoveryAsync(EndpointConfig endpoint, CheckResult result, CancellationToken cancellationToken = default) => Record("recovery");

        private Task Record(string notification)
        {
            Notifications.Add(notification);
            events.Add(notification);
            return Exception is null ? Task.CompletedTask : Task.FromException(Exception);
        }
    }
}
