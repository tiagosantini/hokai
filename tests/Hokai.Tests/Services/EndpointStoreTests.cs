using System.Text;
using System.Text.Json;
using Hokai.Models;
using Hokai.Services;

namespace Hokai.Tests.Services;

public sealed class EndpointStoreTests : IDisposable
{
    private readonly string _dataDirectory = Path.Combine(
        Path.GetTempPath(), $"hokai-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task GetAllAsync_MissingFile_ReturnsEmptyWithoutCreatingDirectory()
    {
        var store = new EndpointStore(_dataDirectory);

        var endpoints = await store.GetAllAsync();

        Assert.Empty(endpoints);
        Assert.False(Directory.Exists(_dataDirectory));
    }

    [Fact]
    public async Task AddAsync_NewEndpoint_PersistsDocumentedJson()
    {
        var store = new EndpointStore(_dataDirectory);

        await store.AddAsync(CreateEndpoint("one"));

        var path = Path.Combine(_dataDirectory, "endpoints.json");
        var bytes = await File.ReadAllBytesAsync(path);
        using var document = JsonDocument.Parse(bytes);
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Equal("one", document.RootElement[0].GetProperty("id").GetString());
        Assert.Contains(Environment.NewLine, Encoding.UTF8.GetString(bytes));
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
    }

    [Fact]
    public async Task AddAsync_MultipleEndpoints_PreservesEveryEndpoint()
    {
        var store = new EndpointStore(_dataDirectory);
        await store.AddAsync(CreateEndpoint("one"));

        await store.AddAsync(CreateEndpoint("two"));
        var endpoints = await store.GetAllAsync();

        Assert.Equal(["one", "two"], endpoints.Select(endpoint => endpoint.Id));
        Assert.Equal(CreateEndpoint("two").Url, (await store.GetByIdAsync("two"))?.Url);
    }

    [Fact]
    public async Task AddAsync_DuplicateId_ThrowsWithoutChangingFile()
    {
        var store = new EndpointStore(_dataDirectory);
        await store.AddAsync(CreateEndpoint("one"));
        var path = Path.Combine(_dataDirectory, "endpoints.json");
        var original = await File.ReadAllBytesAsync(path);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.AddAsync(CreateEndpoint("one")));

        Assert.Equal(original, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task RemoveAsync_ExistingId_RemovesOnlyMatchingEndpoint()
    {
        var store = new EndpointStore(_dataDirectory);
        await store.AddAsync(CreateEndpoint("one"));
        await store.AddAsync(CreateEndpoint("two"));

        var removed = await store.RemoveAsync("one");

        Assert.True(removed);
        Assert.Equal("two", Assert.Single(await store.GetAllAsync()).Id);
    }

    [Fact]
    public async Task RemoveAsync_UnknownId_ReturnsFalseWithoutChangingFile()
    {
        var store = new EndpointStore(_dataDirectory);
        await store.AddAsync(CreateEndpoint("one"));
        var path = Path.Combine(_dataDirectory, "endpoints.json");
        var original = await File.ReadAllBytesAsync(path);

        var removed = await store.RemoveAsync("missing");

        Assert.False(removed);
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task AddAsync_MalformedFile_ThrowsWithoutReplacingFile()
    {
        Directory.CreateDirectory(_dataDirectory);
        var path = Path.Combine(_dataDirectory, "endpoints.json");
        await File.WriteAllTextAsync(path, "not-json");
        var store = new EndpointStore(_dataDirectory);

        await Assert.ThrowsAsync<JsonException>(() => store.AddAsync(CreateEndpoint("one")));

        Assert.Equal("not-json", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task AddAsync_ConcurrentCalls_PersistsEveryEndpoint()
    {
        var stores = new[] { new EndpointStore(_dataDirectory), new EndpointStore(_dataDirectory) };

        await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(index => stores[index % stores.Length].AddAsync(CreateEndpoint(index.ToString()))));

        Assert.Equal(20, (await stores[0].GetAllAsync()).Count);
    }

    [Fact]
    public async Task AddAsync_CanceledToken_DoesNotCreateFile()
    {
        var store = new EndpointStore(_dataDirectory);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.AddAsync(CreateEndpoint("one"), cancellation.Token));

        Assert.False(Directory.Exists(_dataDirectory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }
    }

    private static EndpointConfig CreateEndpoint(string id) => new()
    {
        Id = id,
        Url = new Uri($"https://example.com/{id}"),
        Interval = TimeSpan.FromMinutes(5),
        Timeout = TimeSpan.FromSeconds(30),
        Method = "GET",
        ExpectedStatus = 200,
        CreatedAt = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero)
    };
}
