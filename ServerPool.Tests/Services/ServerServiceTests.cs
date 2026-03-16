using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServerPool.Core.DTOs;
using ServerPool.Core.Interfaces;
using ServerPool.Core.Models;
using ServerPool.Infrastructure.Data;
using ServerPool.Infrastructure.Services;
using Xunit;

namespace ServerPool.Tests.Services;

public class ServerServiceTests : IDisposable
{
    private readonly ServerPoolDbContext _context;
    private readonly IServerService _serverService;
    private readonly ILogger<ServerService> _logger;

    public ServerServiceTests()
    {
        var options = new DbContextOptionsBuilder<ServerPoolDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ServerPoolDbContext(options);
        _logger = new LoggerFactory().CreateLogger<ServerService>();
        _serverService = new ServerService(_context, _logger);
    }

    [Fact]
    public async Task AddServerAsync_ShouldAddServerSuccessfully()
    {
        
        var request = new AddServerRequest
        {
            OperatingSystem = "Windows Server 2022",
            MemoryGB = 32,
            DiskGB = 500,
            CpuCores = 8,
            IsOnline = true
        };

        var server = await _serverService.AddServerAsync(request);

        server.Should().NotBeNull();
        server.OperatingSystem.Should().Be(request.OperatingSystem);
        server.MemoryGB.Should().Be(request.MemoryGB);
        server.Status.Should().Be(ServerStatus.Available);
    }

    [Fact]
    public async Task AddServerAsync_OfflineServer_ShouldSetStatusToPoweringOn()
    {
        
        var request = new AddServerRequest
        {
            OperatingSystem = "Ubuntu 22.04",
            MemoryGB = 16,
            DiskGB = 250,
            CpuCores = 4,
            IsOnline = false
        };

        var server = await _serverService.AddServerAsync(request);

        server.Status.Should().Be(ServerStatus.PoweringOn);
        server.PowerOnRequestedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchAvailableServersAsync_ShouldReturnOnlyAvailableServers()
    {
       
        var availableServer = new Server
        {
            Id = Guid.NewGuid(),
            OperatingSystem = "Windows Server 2022",
            MemoryGB = 32,
            DiskGB = 500,
            CpuCores = 8,
            Status = ServerStatus.Available
        };

        var allocatedServer = new Server
        {
            Id = Guid.NewGuid(),
            OperatingSystem = "Ubuntu 22.04",
            MemoryGB = 16,
            DiskGB = 250,
            CpuCores = 4,
            Status = ServerStatus.Allocated
        };

        _context.Servers.AddRange(availableServer, allocatedServer);
        await _context.SaveChangesAsync();

        var result = await _serverService.SearchAvailableServersAsync(new SearchServersRequest());

        result.Should().Contain(s => s.Id == availableServer.Id);
        result.Should().NotContain(s => s.Id == allocatedServer.Id);
    }

    [Fact]
    public async Task SearchAvailableServersAsync_ShouldFilterByMemory()
    {
        
        var server1 = new Server
        {
            Id = Guid.NewGuid(),
            OperatingSystem = "Windows Server 2022",
            MemoryGB = 32,
            DiskGB = 500,
            CpuCores = 8,
            Status = ServerStatus.Available
        };

        var server2 = new Server
        {
            Id = Guid.NewGuid(),
            OperatingSystem = "Ubuntu 22.04",
            MemoryGB = 16,
            DiskGB = 250,
            CpuCores = 4,
            Status = ServerStatus.Available
        };

        _context.Servers.AddRange(server1, server2);
        await _context.SaveChangesAsync();

        var result = await _serverService.SearchAvailableServersAsync(
            new SearchServersRequest { MinMemoryGB = 20 });

        result.Should().Contain(s => s.Id == server1.Id);
        result.Should().NotContain(s => s.Id == server2.Id);
    }

    [Fact]
    public async Task AllocateServerAsync_ShouldAllocateAvailableServer()
    {
        
        var server = new Server
        {
            Id = Guid.NewGuid(),
            OperatingSystem = "Windows Server 2022",
            MemoryGB = 32,
            DiskGB = 500,
            CpuCores = 8,
            Status = ServerStatus.Available
        };

        _context.Servers.Add(server);
        await _context.SaveChangesAsync();

        var result = await _serverService.AllocateServerAsync(server.Id, "user1");

        result.Should().NotBeNull();
        result!.Status.Should().Be(ServerStatus.Allocated);
        result.AllocatedTo.Should().Be("user1");
        result.AllocatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AllocateServerAsync_ShouldNotAllocateAllocatedServer()
    {
        
        var server = new Server
        {
            Id = Guid.NewGuid(),
            OperatingSystem = "Windows Server 2022",
            MemoryGB = 32,
            DiskGB = 500,
            CpuCores = 8,
            Status = ServerStatus.Allocated,
            AllocatedTo = "user1"
        };

        _context.Servers.Add(server);
        await _context.SaveChangesAsync();

        var result = await _serverService.AllocateServerAsync(server.Id, "user2");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ReleaseServerAsync_ShouldReleaseAllocatedServer()
    {
      
        var server = new Server
        {
            Id = Guid.NewGuid(),
            OperatingSystem = "Windows Server 2022",
            MemoryGB = 32,
            DiskGB = 500,
            CpuCores = 8,
            Status = ServerStatus.Allocated,
            AllocatedTo = "user1",
            AllocatedAt = DateTime.UtcNow
        };

        _context.Servers.Add(server);
        await _context.SaveChangesAsync();

        var result = await _serverService.ReleaseServerAsync(server.Id);

        result.Should().BeTrue();
        var updatedServer = await _context.Servers.FindAsync(server.Id);
        updatedServer!.Status.Should().Be(ServerStatus.Available);
        updatedServer.AllocatedTo.Should().BeNull();
        updatedServer.AllocatedAt.Should().BeNull();
    }

    [Fact]
    public async Task IsServerReadyAsync_ShouldReturnTrueForAvailableServer()
    {
        
        var server = new Server
        {
            Id = Guid.NewGuid(),
            OperatingSystem = "Windows Server 2022",
            MemoryGB = 32,
            DiskGB = 500,
            CpuCores = 8,
            Status = ServerStatus.Available
        };

        _context.Servers.Add(server);
        await _context.SaveChangesAsync();

        var result = await _serverService.IsServerReadyAsync(server.Id);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AllocateServerAsync_ConcurrentRequests_ShouldHandleCorrectly()
    {       
        var server = new Server
        {
            Id = Guid.NewGuid(),
            OperatingSystem = "Windows Server 2022",
            MemoryGB = 32,
            DiskGB = 500,
            CpuCores = 8,
            Status = ServerStatus.Available
        };

        _context.Servers.Add(server);
        await _context.SaveChangesAsync();

        var tasks = Enumerable.Range(0, 10)
            .Select(i => _serverService.AllocateServerAsync(server.Id, $"user{i}"))
            .ToArray();

        var results = await Task.WhenAll<Server?>(tasks);

        var successful = results.Count(r => r != null);
        successful.Should().Be(1);
    }

    [Fact]
    public async Task AllocateServerAsync_OfflineServer_ShouldRequestPowerOn()
    {
      
        var server = new Server
        {
            Id = Guid.NewGuid(),
            OperatingSystem = "Windows Server 2022",
            MemoryGB = 32,
            DiskGB = 500,
            CpuCores = 8,
            Status = ServerStatus.Offline
        };

        _context.Servers.Add(server);
        await _context.SaveChangesAsync();

        var result = await _serverService.AllocateServerAsync(server.Id, "user1");

        result.Should().BeNull(); 
        var updatedServer = await _context.Servers.FindAsync(server.Id);
        updatedServer!.Status.Should().Be(ServerStatus.PoweringOn);
        updatedServer.PowerOnRequestedAt.Should().NotBeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
