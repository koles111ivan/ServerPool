using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServerPool.Core.DTOs;
using ServerPool.Core.Interfaces;
using ServerPool.Core.Models;
using ServerPool.Infrastructure.Data;
using System.Collections.Concurrent;

namespace ServerPool.Infrastructure.Services;

public class ServerService : IServerService
{
    private readonly ServerPoolDbContext _context;
    private readonly ILogger<ServerService> _logger;
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public ServerService(ServerPoolDbContext context, ILogger<ServerService> logger)
    {
        _context = context;
        _logger = logger;
    }

    private SemaphoreSlim GetLock(Guid serverId)
    {
        return _locks.GetOrAdd(serverId, _ => new SemaphoreSlim(1, 1));
    }

    public async Task<Server> AddServerAsync(AddServerRequest request)
    {
        _logger.LogInformation("Adding new server: OS={OS}, Memory={Memory}GB, Disk={Disk}GB, Cores={Cores}",
            request.OperatingSystem, request.MemoryGB, request.DiskGB, request.CpuCores);

        var server = new Server
        {
            Id = Guid.NewGuid(),
            OperatingSystem = request.OperatingSystem,
            MemoryGB = request.MemoryGB,
            DiskGB = request.DiskGB,
            CpuCores = request.CpuCores,
            Status = request.IsOnline ? ServerStatus.Available : ServerStatus.PoweringOn,
            PowerOnRequestedAt = request.IsOnline ? null : DateTime.UtcNow
        };

        _context.Servers.Add(server);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Server added successfully: Id={ServerId}", server.Id);
        return server;
    }

    public async Task<IEnumerable<Server>> SearchAvailableServersAsync(SearchServersRequest request)
    {
        _logger.LogInformation("Searching available servers with filters: OS={OS}, MinMemory={Memory}, MinDisk={Disk}, MinCores={Cores}",
            request.OperatingSystem, request.MinMemoryGB, request.MinDiskGB, request.MinCpuCores);

        var query = _context.Servers.AsQueryable();

        query = query.Where(s => s.Status == ServerStatus.Available ||
                                 (s.Status == ServerStatus.PoweringOn && 
                                  s.PowerOnRequestedAt.HasValue &&
                                  DateTime.UtcNow >= s.PowerOnRequestedAt.Value.AddMinutes(5)));

        if (!string.IsNullOrEmpty(request.OperatingSystem))
        {
            query = query.Where(s => s.OperatingSystem.Contains(request.OperatingSystem));
        }

        if (request.MinMemoryGB.HasValue)
        {
            query = query.Where(s => s.MemoryGB >= request.MinMemoryGB.Value);
        }

        if (request.MinDiskGB.HasValue)
        {
            query = query.Where(s => s.DiskGB >= request.MinDiskGB.Value);
        }

        if (request.MinCpuCores.HasValue)
        {
            query = query.Where(s => s.CpuCores >= request.MinCpuCores.Value);
        }

        var servers = await query.ToListAsync();
        _logger.LogInformation("Found {Count} available servers", servers.Count);
        return servers;
    }

    public async Task<Server?> AllocateServerAsync(Guid serverId, string allocatedTo)
    {
        var semaphore = GetLock(serverId);
        await semaphore.WaitAsync();

        try
        {
            _logger.LogInformation("Attempting to allocate server: ServerId={ServerId}, AllocatedTo={AllocatedTo}",
                serverId, allocatedTo);

            var server = await _context.Servers.FindAsync(serverId);
            if (server == null)
            {
                _logger.LogWarning("Server not found: ServerId={ServerId}", serverId);
                return null;
            }

            if (server.Status == ServerStatus.Offline)
            {
                _logger.LogInformation("Server is offline, requesting power on: ServerId={ServerId}", serverId);
                server.Status = ServerStatus.PoweringOn;
                server.PowerOnRequestedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogWarning("Server is powering on, will be ready in 5 minutes: ServerId={ServerId}", serverId);
                return null;
            }

            if (server.Status == ServerStatus.PoweringOn)
            {
                if (server.PowerOnRequestedAt.HasValue &&
                    DateTime.UtcNow < server.PowerOnRequestedAt.Value.AddMinutes(5))
                {
                    _logger.LogWarning("Server is still powering on: ServerId={ServerId}", serverId);
                    return null;
                }
                server.Status = ServerStatus.Available;
            }

            if (server.Status != ServerStatus.Available)
            {
                _logger.LogWarning("Server is not available: ServerId={ServerId}, Status={Status}",
                    serverId, server.Status);
                return null;
            }

            server.Status = ServerStatus.Allocated;
            server.AllocatedAt = DateTime.UtcNow;
            server.AllocatedTo = allocatedTo;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Server allocated successfully: ServerId={ServerId}, AllocatedTo={AllocatedTo}",
                serverId, allocatedTo);

            return server;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<bool> ReleaseServerAsync(Guid serverId)
    {
        var semaphore = GetLock(serverId);
        await semaphore.WaitAsync();

        try
        {
            _logger.LogInformation("Attempting to release server: ServerId={ServerId}", serverId);

            var server = await _context.Servers.FindAsync(serverId);
            if (server == null)
            {
                _logger.LogWarning("Server not found: ServerId={ServerId}", serverId);
                return false;
            }

            if (server.Status != ServerStatus.Allocated)
            {
                _logger.LogWarning("Server is not allocated: ServerId={ServerId}, Status={Status}",
                    serverId, server.Status);
                return false;
            }

            server.Status = ServerStatus.Available;
            server.AllocatedAt = null;
            server.AllocatedTo = null;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Server released successfully: ServerId={ServerId}", serverId);
            return true;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<Server?> GetServerByIdAsync(Guid serverId)
    {
        return await _context.Servers.FindAsync(serverId);
    }

    public async Task<bool> IsServerReadyAsync(Guid serverId)
    {
        var server = await _context.Servers.FindAsync(serverId);
        if (server == null)
            return false;

        if (server.Status == ServerStatus.Available)
            return true;

        if (server.Status == ServerStatus.PoweringOn && 
            server.PowerOnRequestedAt.HasValue &&
            DateTime.UtcNow >= server.PowerOnRequestedAt.Value.AddMinutes(5))
        {
            server.Status = ServerStatus.Available;
            await _context.SaveChangesAsync();
            return true;
        }

        return false;
    }

    public async Task<IEnumerable<Server>> GetAllServersAsync()
    {
        return await _context.Servers.ToListAsync();
    }
}
