using System.Net;
using Hokai.Models;
using Hokai.Services;

namespace Hokai.Tests.Services;

public sealed class HealthCheckServiceTests
{
    [Fact]
    public async Task CheckAsync_ExpectedStatus_ReturnsUpResult()
    {
        var time = new ManualTimeProvider();
        var service = CreateService(_ =>
        {
            time.Advance(TimeSpan.FromMilliseconds(125));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, time);

        var result = await service.CheckAsync(CreateEndpoint());

        Assert.True(result.IsUp);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(125, result.ResponseTimeMs);
        Assert.Equal(time.GetUtcNow(), result.Timestamp);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task CheckAsync_UnexpectedStatus_ReturnsDownWithActualStatus()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var result = await service.CheckAsync(CreateEndpoint());

        Assert.False(result.IsUp);
        Assert.Equal(503, result.StatusCode);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task CheckAsync_Method_UsesConfiguredMethod(string method)
    {
        HttpMethod? observedMethod = null;
        var service = CreateService(request =>
        {
            observedMethod = request.Method;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await service.CheckAsync(CreateEndpoint(method: method));

        Assert.Equal(method, observedMethod?.Method);
    }

    [Fact]
    public async Task CheckAsync_TransportFailure_ReturnsDownResult()
    {
        var service = CreateService(_ => throw new HttpRequestException("Connection refused"));

        var result = await service.CheckAsync(CreateEndpoint());

        Assert.False(result.IsUp);
        Assert.Null(result.StatusCode);
        Assert.Equal("Connection refused", result.Error);
    }

    [Fact]
    public async Task CheckAsync_LongTransportError_TruncatesPersistedMessage()
    {
        var service = CreateService(_ => throw new HttpRequestException(new string('x', 2048)));

        var result = await service.CheckAsync(CreateEndpoint());

        Assert.Equal(1024, result.Error?.Length);
    }

    [Fact]
    public async Task CheckAsync_EndpointTimeout_ReturnsDownResult()
    {
        var service = CreateService(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var result = await service.CheckAsync(CreateEndpoint(timeout: TimeSpan.FromMilliseconds(10)));

        Assert.False(result.IsUp);
        Assert.Null(result.StatusCode);
        Assert.Equal("The request timed out.", result.Error);
    }

    [Fact]
    public async Task CheckAsync_CallerCancellation_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CheckAsync(CreateEndpoint(), cancellation.Token));
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("file:///tmp/check")]
    public async Task CheckAsync_NonHttpUrl_ThrowsArgumentException(string url)
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CheckAsync(CreateEndpoint(url: url)));
    }

    [Fact]
    public async Task CheckAsync_NonPositiveTimeout_ThrowsArgumentOutOfRangeException()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.CheckAsync(CreateEndpoint(timeout: TimeSpan.Zero)));
    }

    [Fact]
    public async Task CheckAsync_EmptyMethod_ThrowsArgumentException()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CheckAsync(CreateEndpoint(method: " ")));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(600)]
    public async Task CheckAsync_InvalidExpectedStatus_ThrowsArgumentOutOfRangeException(int status)
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.CheckAsync(CreateEndpoint(expectedStatus: status)));
    }

    private static HealthCheckService CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        ManualTimeProvider? timeProvider = null) =>
        CreateService((request, _) => Task.FromResult(handler(request)), timeProvider);

    private static HealthCheckService CreateService(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        ManualTimeProvider? timeProvider = null)
    {
        var client = new HttpClient(new StubHandler(handler))
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        return new HealthCheckService(new StubHttpClientFactory(client), timeProvider ?? new());
    }

    private static EndpointConfig CreateEndpoint(
        string method = "GET",
        string url = "https://example.com/health",
        TimeSpan? timeout = null,
        int expectedStatus = 200) => new()
    {
        Id = "endpoint",
        Url = new Uri(url),
        Interval = TimeSpan.FromMinutes(1),
        Timeout = timeout ?? TimeSpan.FromSeconds(30),
        Method = method,
        ExpectedStatus = expectedStatus,
        CreatedAt = DateTimeOffset.UnixEpoch
    };

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request, cancellationToken);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        private long _timestamp;

        public override long TimestampFrequency => 1000;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan duration)
        {
            _utcNow += duration;
            _timestamp += (long)duration.TotalMilliseconds;
        }
    }
}
