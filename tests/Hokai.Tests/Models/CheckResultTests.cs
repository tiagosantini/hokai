using System.Text.Json;
using Hokai.Models;

namespace Hokai.Tests.Models;

public sealed class CheckResultTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Deserialize_SuccessfulCheck_CreatesCheckResult()
    {
        const string json = """
            {
              "endpointId": "a1b2c3d4",
              "timestamp": "2026-07-10T12:05:00Z",
              "isUp": true,
              "statusCode": 200,
              "responseTimeMs": 145,
              "error": null
            }
            """;

        var result = JsonSerializer.Deserialize<CheckResult>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Equal("a1b2c3d4", result.EndpointId);
        Assert.Equal(DateTimeOffset.Parse("2026-07-10T12:05:00Z"), result.Timestamp);
        Assert.True(result.IsUp);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(145, result.ResponseTimeMs);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Deserialize_FailedCheck_AllowsNullStatusCode()
    {
        const string json = """
            {
              "endpointId": "a1b2c3d4",
              "timestamp": "2026-07-10T12:05:00Z",
              "isUp": false,
              "statusCode": null,
              "responseTimeMs": 30000,
              "error": "The request timed out."
            }
            """;

        var result = JsonSerializer.Deserialize<CheckResult>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.False(result.IsUp);
        Assert.Null(result.StatusCode);
        Assert.Equal("The request timed out.", result.Error);
    }

    [Fact]
    public void Serialize_FailedCheck_WritesNullStatusAndError()
    {
        var result = new CheckResult
        {
            EndpointId = "a1b2c3d4",
            Timestamp = new DateTimeOffset(2026, 7, 10, 12, 5, 0, TimeSpan.Zero),
            IsUp = false,
            StatusCode = null,
            ResponseTimeMs = 30000,
            Error = "The request timed out."
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(JsonValueKind.Null, root.GetProperty("statusCode").ValueKind);
        Assert.Equal("The request timed out.", root.GetProperty("error").GetString());
    }
}
