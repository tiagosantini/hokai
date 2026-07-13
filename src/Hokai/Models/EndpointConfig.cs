namespace Hokai.Models;

public sealed class EndpointConfig
{
    public required string Id { get; init; }

    public required Uri Url { get; init; }

    public required TimeSpan Interval { get; init; }

    public required TimeSpan Timeout { get; init; }

    public required string Method { get; init; }

    public required int ExpectedStatus { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
