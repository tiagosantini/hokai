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

    [Fact]
    public async Task RemoveOlderThanAsync_MissingFile_DoesNotCreateDirectory()
    {
        await CreateStore().RemoveOlderThanAsync(TimeSpan.FromDays(30));

        Assert.False(Directory.Exists(_dataDirectory));
    }

    [Fact]
    public async Task RemoveOlderThanAsync_ExpiredResults_RemovesOnlyBeforeCutoff()
    {
        var store = CreateStore();
        await store.AppendAsync(CreateResult("expired", Now.AddDays(-30).AddTicks(-1)));
        await store.AppendAsync(CreateResult("cutoff", Now.AddDays(-30)));
        await store.AppendAsync(CreateResult("recent", Now.AddDays(-1)));

        await store.RemoveOlderThanAsync(TimeSpan.FromDays(30));

        Assert.Equal(["cutoff", "recent"],
            (await ReadPersistedAsync()).Select(result => result.EndpointId));
    }

    [Fact]
    public async Task RemoveOlderThanAsync_NoExpiredResults_DoesNotRewriteFile()
    {
        var store = CreateStore();
        await store.AppendAsync(CreateResult("recent", Now));
        var path = Path.Combine(_dataDirectory, "checks.json");
        var originalWriteTime = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, originalWriteTime);

        await store.RemoveOlderThanAsync(TimeSpan.FromDays(30));

        Assert.Equal(originalWriteTime, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public async Task RemoveOlderThanAsync_NegativeRetention_ThrowsArgumentOutOfRangeException()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateStore().RemoveOlderThanAsync(TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public async Task GetBatchSummariesAsync_MultipleEndpoints_ReturnsGroupedResults()
    {
        var store = CreateStore();
        await store.AppendAsync(CreateResult("one", Now.AddHours(-1), isUp: true));
        await store.AppendAsync(CreateResult("one", Now, isUp: false));
        await store.AppendAsync(CreateResult("two", Now.AddMinutes(-30), isUp: true));

        var summaries = await store.GetBatchSummariesAsync(TimeSpan.FromHours(24));

        Assert.Equal(2, summaries.Count);
        var one = summaries.Single(s => s.EndpointId == "one");
        var two = summaries.Single(s => s.EndpointId == "two");
        Assert.Equal(50d, one.Uptime);
        Assert.Equal(Now, one.LastCheck!.Timestamp);
        Assert.Equal(100d, two.Uptime);
        Assert.Equal(Now.AddMinutes(-30), two.LastCheck!.Timestamp);
    }

    [Fact]
    public async Task GetBatchSummariesAsync_EmptyFile_ReturnsEmptyList()
    {
        var summaries = await CreateStore().GetBatchSummariesAsync(TimeSpan.FromHours(24));

        Assert.Empty(summaries);
    }

    [Fact]
    public async Task GetBatchSummariesAsync_RespectsWindowCutoff()
    {
        var store = CreateStore();
        await store.AppendAsync(CreateResult("one", Now.AddHours(-25), isUp: true));
        await store.AppendAsync(CreateResult("one", Now, isUp: true));

        var summaries = await store.GetBatchSummariesAsync(TimeSpan.FromHours(24));

        var one = summaries.Single();
        Assert.Equal(100d, one.Uptime);
    }

    [Fact]
    public async Task GetBatchSummariesAsync_NonPositiveWindow_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateStore().GetBatchSummariesAsync(TimeSpan.Zero));
    }

    [Fact]
    public async Task GetBatchSummariesAsync_LastCheckOutsideWindow_StillReturned()
    {
        var store = CreateStore();
        await store.AppendAsync(CreateResult("one", Now.AddHours(-25), isUp: true));
        await store.AppendAsync(CreateResult("one", Now.AddMinutes(-30), isUp: true));

        var summaries = await store.GetBatchSummariesAsync(TimeSpan.FromHours(24));

        // Last check should be the most recent in full history, not just the window.
        Assert.Equal(Now.AddMinutes(-30), summaries.Single().LastCheck!.Timestamp);
        // Uptime should consider only the window (30-min check is inside, 25h is not).
        Assert.Equal(100d, summaries.Single().Uptime);
    }

    [Fact]
    public async Task AppendAndRead_RoundTripsThroughSourceGeneratedContext()
    {
        var store = CreateStore();
        await store.AppendAsync(CreateResult("abc", Now, isUp: true));

        var results = await store.GetBatchSummariesAsync(TimeSpan.FromHours(24));

        Assert.Single(results);
        Assert.Equal("abc", results[0].EndpointId);
    }

    [Fact]
    public async Task ReadEmptyFile_ReturnsEmptyList()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"hokai-empty-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempPath);
            var filePath = Path.Combine(tempPath, "checks.json");
            File.WriteAllText(filePath, "[]");

            var store = new CheckStore(tempPath, new FixedTimeProvider(Now));

            var result = await store.GetBatchSummariesAsync(TimeSpan.FromHours(24));

            Assert.Empty(result);
        }
        finally
        {
            try { Directory.Delete(tempPath, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task RemoveOlderThanAsync_MalformedFile_ThrowsWithoutReplacingFile()
    {
        Directory.CreateDirectory(_dataDirectory);
        var path = Path.Combine(_dataDirectory, "checks.json");
        await File.WriteAllTextAsync(path, "not-json");

        await Assert.ThrowsAsync<JsonException>(() =>
            CreateStore().RemoveOlderThanAsync(TimeSpan.FromDays(30)));

        Assert.Equal("not-json", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task RemoveOlderThanAsync_ConcurrentAppends_PreservesEveryRecentResult()
    {
        var stores = new[] { CreateStore(), CreateStore() };
        await stores[0].AppendAsync(CreateResult("expired", Now.AddDays(-31)));
        var appends = Enumerable.Range(0, 20)
            .Select(index => stores[index % stores.Length].AppendAsync(
                CreateResult(index.ToString(), Now)))
            .Cast<Task>();
        var cleanups = Enumerable.Range(0, 5)
            .Select(index => stores[index % stores.Length]
                .RemoveOlderThanAsync(TimeSpan.FromDays(30)));

        await Task.WhenAll(appends.Concat(cleanups));

        var persisted = await ReadPersistedAsync();
        Assert.Equal(20, persisted.Count);
        Assert.DoesNotContain(persisted, result => result.EndpointId == "expired");
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
