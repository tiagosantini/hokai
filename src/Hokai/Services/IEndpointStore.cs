using Hokai.Models;

namespace Hokai.Services;

public interface IEndpointStore
{
    Task<IReadOnlyList<EndpointConfig>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<EndpointConfig?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task AddAsync(EndpointConfig config, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default);
}
