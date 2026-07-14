namespace Hokai.Models;

public sealed class CheckResult
{
    public required string EndpointId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required bool IsUp { get; init; }

    public int? StatusCode { get; init; }

    public required long ResponseTimeMs { get; init; }

    public string? Error { get; init; }
}
