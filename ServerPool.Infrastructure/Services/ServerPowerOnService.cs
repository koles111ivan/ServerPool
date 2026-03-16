using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServerPool.Core.Models;
using ServerPool.Infrastructure.Data;

namespace ServerPool.Infrastructure.Services;

public class ServerPowerOnService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ServerPowerOnService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _powerOnDuration = TimeSpan.FromMinutes(5);

    public ServerPowerOnService(
        IServiceProvider serviceProvider,
        ILogger<ServerPowerOnService> logger)
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
                await ProcessPowerOnRequestsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in server power-on service");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task ProcessPowerOnRequestsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServerPoolDbContext>();

        var serversReady = await context.Servers
            .Where(s => s.Status == ServerStatus.PoweringOn &&
                       s.PowerOnRequestedAt.HasValue &&
                       DateTime.UtcNow >= s.PowerOnRequestedAt.Value.Add(_powerOnDuration))
            .ToListAsync();

        foreach (var server in serversReady)
        {
            _logger.LogInformation("Server is now ready: ServerId={ServerId}, PowerOnRequestedAt={RequestedAt}",
                server.Id, server.PowerOnRequestedAt);

            server.Status = ServerStatus.Available;
        }

        if (serversReady.Any())
        {
            await context.SaveChangesAsync();
            _logger.LogInformation("Power-on completed for {Count} servers", serversReady.Count);
        }
    }
}
