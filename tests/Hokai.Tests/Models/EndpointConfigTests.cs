using System.Text.Json;
using Hokai.Models;

namespace Hokai.Tests.Models;

public sealed class EndpointConfigTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Deserialize_DocumentedJson_CreatesEndpointConfig()
    {
        const string json = """
            {
              "id": "a1b2c3d4",
              "url": "https://api.example.com/health",
              "interval": "00:05:00",
              "timeout": "00:00:30",
              "method": "GET",
              "expectedStatus": 200,
              "createdAt": "2026-07-10T12:00:00Z"
            }
            """;

        var endpoint = JsonSerializer.Deserialize<EndpointConfig>(json, JsonOptions);

        Assert.NotNull(endpoint);
        Assert.Equal("a1b2c3d4", endpoint.Id);
        Assert.Equal(new Uri("https://api.example.com/health"), endpoint.Url);
        Assert.Equal(TimeSpan.FromMinutes(5), endpoint.Interval);
        Assert.Equal(TimeSpan.FromSeconds(30), endpoint.Timeout);
        Assert.Equal("GET", endpoint.Method);
        Assert.Equal(200, endpoint.ExpectedStatus);
        Assert.Equal(DateTimeOffset.Parse("2026-07-10T12:00:00Z"), endpoint.CreatedAt);
    }

    [Fact]
    public void Serialize_EndpointConfig_UsesDocumentedCamelCaseContract()
    {
        var endpoint = new EndpointConfig
        {
            Id = "a1b2c3d4",
            Url = new Uri("https://api.example.com/health"),
            Interval = TimeSpan.FromMinutes(5),
            Timeout = TimeSpan.FromSeconds(30),
            Method = "GET",
            ExpectedStatus = 200,
            CreatedAt = new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero)
        };

        var json = JsonSerializer.Serialize(endpoint, JsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("a1b2c3d4", root.GetProperty("id").GetString());
        Assert.Equal("https://api.example.com/health", root.GetProperty("url").GetString());
        Assert.Equal("GET", root.GetProperty("method").GetString());
        Assert.Equal(200, root.GetProperty("expectedStatus").GetInt32());
    }

    [Fact]
    public void Serialize_EndpointConfig_PreservesTimeSpanAndCreatedAt()
    {
        var endpoint = new EndpointConfig
        {
            Id = "a1b2c3d4",
            Url = new Uri("https://api.example.com/health"),
            Interval = TimeSpan.FromMinutes(5),
            Timeout = TimeSpan.FromSeconds(30),
            Method = "GET",
            ExpectedStatus = 200,
            CreatedAt = new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero)
        };

        var json = JsonSerializer.Serialize(endpoint, JsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("00:05:00", root.GetProperty("interval").GetString());
        Assert.Equal("00:00:30", root.GetProperty("timeout").GetString());
        Assert.Equal("2026-07-10T12:00:00+00:00", root.GetProperty("createdAt").GetString());
    }
}
