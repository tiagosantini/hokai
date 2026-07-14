using Hokai.Models;
using Hokai.Serialization;

namespace Hokai.Services;

public sealed class EndpointStore : IEndpointStore
{
    private readonly string _path;

    public EndpointStore(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _path = Path.Combine(dataDirectory, "endpoints.json");
    }

    public async Task<IReadOnlyList<EndpointConfig>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        await AtomicJsonFile.ReadAsync(_path, HokaiJsonContext.Default.ListEndpointConfig, cancellationToken);

    public async Task<EndpointConfig?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var endpoints = await AtomicJsonFile.ReadAsync(_path, HokaiJsonContext.Default.ListEndpointConfig, cancellationToken);
        return endpoints.FirstOrDefault(endpoint => string.Equals(endpoint.Id, id, StringComparison.Ordinal));
    }

    public async Task AddAsync(EndpointConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        await AtomicJsonFile.MutateAsync(
            _path,
            HokaiJsonContext.Default.ListEndpointConfig,
            (List<EndpointConfig> endpoints) =>
            {
                if (endpoints.Any(endpoint =>
                    string.Equals(endpoint.Id, config.Id, StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException($"Endpoint '{config.Id}' already exists.");
                }

                endpoints.Add(config);
                return (true, true);
            },
            cancellationToken);
    }

    public Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return AtomicJsonFile.MutateAsync(
            _path,
            HokaiJsonContext.Default.ListEndpointConfig,
            (List<EndpointConfig> endpoints) =>
            {
                var removed = endpoints.RemoveAll(endpoint =>
                    string.Equals(endpoint.Id, id, StringComparison.Ordinal)) > 0;

                return (removed, removed);
            },
            cancellationToken);
    }
}
