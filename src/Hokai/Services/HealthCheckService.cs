using Hokai.Models;

namespace Hokai.Services;

public sealed class HealthCheckService(
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider) : IHealthCheckService
{
    private const string ClientName = "Hokai.HealthChecks";

    public async Task<CheckResult> CheckAsync(
        EndpointConfig endpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        Validate(endpoint);
        cancellationToken.ThrowIfCancellationRequested();

        var startedAt = timeProvider.GetTimestamp();

        // Caller cancellation controls worker lifetime; the linked timeout only classifies endpoint latency.
        using var timeout = new CancellationTokenSource(endpoint.Timeout);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeout.Token);
        using var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), endpoint.Url);

        try
        {
            var client = httpClientFactory.CreateClient(ClientName);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                linkedCancellation.Token);
            var statusCode = (int)response.StatusCode;

            return CreateResult(
                endpoint,
                startedAt,
                statusCode == endpoint.ExpectedStatus,
                statusCode,
                error: null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CreateResult(endpoint, startedAt, false, null, "The request timed out.");
        }
        catch (HttpRequestException exception)
        {
            return CreateResult(endpoint, startedAt, false, null, Truncate(exception.Message));
        }
    }

    private CheckResult CreateResult(
        EndpointConfig endpoint,
        long startedAt,
        bool isUp,
        int? statusCode,
        string? error) => new()
    {
        EndpointId = endpoint.Id,
        Timestamp = timeProvider.GetUtcNow(),
        IsUp = isUp,
        StatusCode = statusCode,
        ResponseTimeMs = (long)timeProvider.GetElapsedTime(startedAt).TotalMilliseconds,
        Error = error
    };

    private static void Validate(EndpointConfig endpoint)
    {
        if (!endpoint.Url.IsAbsoluteUri
            || (endpoint.Url.Scheme != Uri.UriSchemeHttp && endpoint.Url.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Endpoint URL must be an absolute HTTP or HTTPS URL.", nameof(endpoint));
        }

        if (endpoint.Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(endpoint), "Endpoint timeout must be positive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint.Method);
        if (endpoint.ExpectedStatus is < 100 or > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(endpoint), "Expected status must be between 100 and 599.");
        }
    }

    private static string Truncate(string message) =>
        message.Length <= 1024 ? message : message[..1024];
}
