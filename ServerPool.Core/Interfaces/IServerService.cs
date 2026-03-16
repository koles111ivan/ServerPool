using ServerPool.Core.DTOs;
using ServerPool.Core.Models;

namespace ServerPool.Core.Interfaces;

public interface IServerService
{
    Task<Server> AddServerAsync(AddServerRequest request);
    Task<IEnumerable<Server>> SearchAvailableServersAsync(SearchServersRequest request);
    Task<Server?> AllocateServerAsync(Guid serverId, string allocatedTo);
    Task<bool> ReleaseServerAsync(Guid serverId);
    Task<Server?> GetServerByIdAsync(Guid serverId);
    Task<bool> IsServerReadyAsync(Guid serverId);
    Task<IEnumerable<Server>> GetAllServersAsync();
}
