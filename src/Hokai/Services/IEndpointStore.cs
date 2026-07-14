using Hokai.Models;

namespace Hokai.Services;

/// <summary>Provides asynchronous access to the persisted endpoint collection.</summary>
public interface IEndpointStore
{
    /// <summary>Returns all endpoints, or an empty collection when the data file does not exist.</summary>
    Task<IReadOnlyList<EndpointConfig>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the endpoint whose identifier matches using ordinal comparison.</summary>
    Task<EndpointConfig?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Adds an endpoint and rejects an identifier that is already persisted.</summary>
    /// <exception cref="InvalidOperationException">The endpoint identifier already exists.</exception>
    Task AddAsync(EndpointConfig config, CancellationToken cancellationToken = default);

    /// <summary>Removes an endpoint without rewriting the file when the identifier is unknown.</summary>
    /// <returns><see langword="true"/> when an endpoint was removed; otherwise, <see langword="false"/>.</returns>
    Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default);
}
