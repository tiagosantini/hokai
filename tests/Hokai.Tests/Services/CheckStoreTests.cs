using System.Text.Json;
using Hokai.Models;
using Hokai.Services;

namespace Hokai.Tests.Services;

public sealed class CheckStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
    private readonly string _dataDirectory = Path.Combine(
        Path.GetTempPath(), $"hokai-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task AppendAsync_NewResult_PersistsDocumentedJson()
    {
        var store = CreateStore();

        await store.AppendAsync(CreateResult("one", Now, isUp: false));

        var path = Path.Combine(_dataDirectory, "checks.json");
        using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(path));
        var result = document.RootElement[0];
        Assert.Equal("one", result.GetProperty("endpointId").GetString());
        Assert.False(result.GetProperty("isUp").GetBoolean());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("statusCode").ValueKind);
        Assert.Equal("Unavailable", result.GetProperty("error").GetString());
    }

    [Fact]
    public async Task AppendAsync_MultipleResults_PreservesOrder()
    {
        var store = CreateStore();
        await store.AppendAsync(CreateResult("one", Now.AddMinutes(-2)));

        await store.AppendAsync(CreateResult("two", Now.AddMinutes(-1)));
        var persisted = await ReadPersistedAsync();

        Assert.Equal(["one", "two"], persisted.Select(result => result.EndpointId));
    }

    [Fact]
    public async Task AppendAsync_ConcurrentCalls_PersistsEveryResult()
    {
        var stores = new[] { CreateStore(), CreateStore() };

        await Task.WhenAll(Enumerable.Range(0, 20).Select(index =>
            stores[index % stores.Length].AppendAsync(
                CreateResult("one", Now.AddSeconds(index)))));

        Assert.Equal(20, (await ReadPersistedAsync()).Count);
    }

    [Fact]
    public async Task AppendAsync_MalformedFile_ThrowsWithoutReplacingFile()
    {
        Directory.CreateDirectory(_dataDirectory);
        var path = Path.Combine(_dataDirectory, "checks.json");
        await File.WriteAllTextAsync(path, "not-json");

        await Assert.ThrowsAsync<JsonException>(() =>
            CreateStore().AppendAsync(CreateResult("one", Now)));

        Assert.Equal("not-json", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task GetLastCheckAsync_OutOfOrderResults_ReturnsGreatestMatchingTimestamp()
    {
        var store = CreateStore();
        await store.AppendAsync(CreateResult("one", Now.AddMinutes(-1)));
        await store.AppendAsync(CreateResult("other", Now));
        await store.AppendAsync(CreateResult("one", Now.AddMinutes(-3)));

        var result = await store.GetLastCheckAsync("one");

        Assert.Equal(Now.AddMinutes(-1), result?.Timestamp);
    }

    [Fact]
    public async Task GetLastCheckAsync_MissingFile_ReturnsNull()
    {
        var result = await CreateStore().GetLastCheckAsync("one");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetUptimeAsync_NoMatchingChecks_ReturnsZero()
    {
        var uptime = await CreateStore().GetUptimeAsync("one", TimeSpan.FromHours(24));

        Assert.Equal(0, uptime);
    }

    [Fact]
    public async Task GetUptimeAsync_Window_FiltersAndCalculatesSuccessfulPercentage()
    {
        var store = CreateStore();
        await store.AppendAsync(CreateResult("one", Now.AddHours(-24), isUp: true));
        await store.AppendAsync(CreateResult("one", Now.AddHours(-1), isUp: false));
        await store.AppendAsync(CreateResult("one", Now, isUp: true));
        await store.AppendAsync(CreateResult("one", Now.AddTicks(1), isUp: false));
        await store.AppendAsync(CreateResult("one", Now.AddHours(-24).AddTicks(-1), isUp: false));
        await store.AppendAsync(CreateResult("other", Now, isUp: false));

        var uptime = await store.GetUptimeAsync("one", TimeSpan.FromHours(24));

        Assert.Equal(200d / 3d, uptime, precision: 10);
    }

    [Fact]
    public async Task GetUptimeAsync_NonPositiveWindow_ThrowsArgumentOutOfRangeException()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateStore().GetUptimeAsync("one", TimeSpan.Zero));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }
    }

    private CheckStore CreateStore() => new(_dataDirectory, new FixedTimeProvider(Now));

    private async Task<List<CheckResult>> ReadPersistedAsync()
    {
        await using var stream = File.OpenRead(Path.Combine(_dataDirectory, "checks.json"));
        return (await JsonSerializer.DeserializeAsync<List<CheckResult>>(
            stream, new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
    }

    private static CheckResult CreateResult(
        string endpointId,
        DateTimeOffset timestamp,
        bool isUp = true) => new()
    {
        EndpointId = endpointId,
        Timestamp = timestamp,
        IsUp = isUp,
        StatusCode = isUp ? 200 : null,
        ResponseTimeMs = 25,
        Error = isUp ? null : "Unavailable"
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
