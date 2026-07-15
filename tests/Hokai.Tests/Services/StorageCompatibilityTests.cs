using Hokai.Services;

namespace Hokai.Tests.Services;

public sealed class StorageCompatibilityTests
{
    private static string FixtureDir =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    [Fact]
    public async Task EndpointsFixture_ReadsAllEndpointsCorrectly()
    {
        var store = new EndpointStore(FixtureDir);

        var endpoints = await store.GetAllAsync();

        Assert.Equal(2, endpoints.Count);

        var one = endpoints.Single(e => e.Id == "a1b2c3d4");
        Assert.Equal(new Uri("https://example.com/health"), one.Url);
        Assert.Equal(TimeSpan.FromSeconds(30), one.Interval);
        Assert.Equal(TimeSpan.FromSeconds(10), one.Timeout);
        Assert.Equal("GET", one.Method);
        Assert.Equal(200, one.ExpectedStatus);

        var two = endpoints.Single(e => e.Id == "e5f6g7h8");
        Assert.Equal(new Uri("https://api.example.com/status"), two.Url);
        Assert.Equal(TimeSpan.FromMinutes(1), two.Interval);
        Assert.Equal(TimeSpan.FromSeconds(5), two.Timeout);
        Assert.Equal("HEAD", two.Method);
    }

    [Fact]
    public async Task EndpointsFixture_GetById_ReturnsCorrectEndpoint()
    {
        var store = new EndpointStore(FixtureDir);

        var endpoint = await store.GetByIdAsync("e5f6g7h8");

        Assert.NotNull(endpoint);
        Assert.Equal("https://api.example.com/status", endpoint.Url.AbsoluteUri);
    }

    [Fact]
    public async Task EndpointsFixture_RemoveThenAdd_PreservesCompatibleFormat()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"hokai-compat-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.Copy(Path.Combine(FixtureDir, "endpoints.json"), Path.Combine(tempDir, "endpoints.json"));

            var store = new EndpointStore(tempDir);
            var removed = await store.RemoveAsync("a1b2c3d4");
            Assert.True(removed);

            var afterRemove = await store.GetAllAsync();
            Assert.Single(afterRemove);
            Assert.Equal("e5f6g7h8", afterRemove[0].Id);

            await store.AddAsync(new Hokai.Models.EndpointConfig
            {
                Id = "i9j0k1l2",
                Url = new Uri("https://new.example.com/ping"),
                Interval = TimeSpan.FromSeconds(45),
                Timeout = TimeSpan.FromSeconds(15),
                Method = "POST",
                ExpectedStatus = 201,
                CreatedAt = new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero)
            });

            var afterAdd = await store.GetAllAsync();
            Assert.Equal(2, afterAdd.Count);
            Assert.Contains(afterAdd, e => e.Id == "i9j0k1l2");
            Assert.Contains(afterAdd, e => e.Id == "e5f6g7h8");

            var roundtripped = afterAdd.Single(e => e.Id == "i9j0k1l2");
            Assert.Equal(201, roundtripped.ExpectedStatus);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    [Fact]
    public async Task ChecksFixture_GetUptime_ComputesCorrectly()
    {
        var now = new DateTimeOffset(2026, 7, 14, 10, 0, 0, TimeSpan.Zero);
        var store = new CheckStore(FixtureDir, new FixedNowProvider(now));

        var a1Uptime = await store.GetUptimeAsync("a1b2c3d4", TimeSpan.FromHours(24));

        Assert.Equal(200d / 3d, a1Uptime, precision: 4);
    }

    [Fact]
    public async Task ChecksFixture_GetBatchSummaries_ComputesCorrectly()
    {
        var now = new DateTimeOffset(2026, 7, 14, 10, 0, 0, TimeSpan.Zero);
        var store = new CheckStore(FixtureDir, new FixedNowProvider(now));

        var summaries = await store.GetBatchSummariesAsync(TimeSpan.FromHours(24));

        Assert.Equal(2, summaries.Count);
        var a1 = summaries.Single(s => s.EndpointId == "a1b2c3d4");
        var e5 = summaries.Single(s => s.EndpointId == "e5f6g7h8");

        Assert.Equal(200d / 3d, a1.Uptime, precision: 4);
        Assert.Equal(50d, e5.Uptime);
        Assert.NotNull(a1.LastCheck);
        Assert.NotNull(e5.LastCheck);
    }

    [Fact]
    public async Task ChecksFixture_AppendThenRead_PreservesExistingRecords()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"hokai-compat-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.Copy(Path.Combine(FixtureDir, "checks.json"), Path.Combine(tempDir, "checks.json"));

            var now = new DateTimeOffset(2026, 7, 14, 10, 0, 0, TimeSpan.Zero);
            var store = new CheckStore(tempDir, new FixedNowProvider(now));

            await store.AppendAsync(new Hokai.Models.CheckResult
            {
                EndpointId = "a1b2c3d4",
                Timestamp = now,
                IsUp = true,
                StatusCode = 200,
                ResponseTimeMs = 25
            });

            var summaries = await store.GetBatchSummariesAsync(TimeSpan.FromHours(24));

            var a1 = summaries.Single(s => s.EndpointId == "a1b2c3d4");
            Assert.Equal(75d, a1.Uptime);
            Assert.Equal(now, a1.LastCheck!.Timestamp);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    private static void SafeDelete(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch { }
    }

    private sealed class FixedNowProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
