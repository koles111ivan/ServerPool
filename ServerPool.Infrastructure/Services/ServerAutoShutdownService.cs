using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServerPool.Core.Models;
using ServerPool.Infrastructure.Data;

namespace ServerPool.Infrastructure.Services;

public class ServerAutoShutdownService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ServerAutoShutdownService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
    private readonly TimeSpan _shutdownAfter = TimeSpan.FromMinutes(20);

    public ServerAutoShutdownService(
        IServiceProvider serviceProvider,
        ILogger<ServerAutoShutdownService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndShutdownServersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in server auto-shutdown service");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CheckAndShutdownServersAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServerPoolDbContext>();

        var serversToShutdown = await context.Servers
            .Where(s => s.Status == ServerStatus.Allocated &&
                       s.AllocatedAt.HasValue &&
                       DateTime.UtcNow >= s.AllocatedAt.Value.Add(_shutdownAfter))
            .ToListAsync();

        foreach (var server in serversToShutdown)
        {
            _logger.LogInformation("Auto-shutting down server: ServerId={ServerId}, AllocatedAt={AllocatedAt}",
                server.Id, server.AllocatedAt);

            server.Status = ServerStatus.Offline;
            server.AllocatedAt = null;
            server.AllocatedTo = null;
            server.PowerOnRequestedAt = null; 
        }

        if (serversToShutdown.Any())
        {
            await context.SaveChangesAsync();
            _logger.LogInformation("Auto-shutdown completed for {Count} servers", serversToShutdown.Count);
        }
    }
}
