using Microsoft.EntityFrameworkCore;
using ServerPool.Core.Interfaces;
using ServerPool.Core.Models;
using ServerPool.Infrastructure.Data;
using ServerPool.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddDbContext<ServerPoolDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        options.UseInMemoryDatabase("ServerPoolDb");
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
});

builder.Services.AddScoped<IServerService, ServerService>();

builder.Services.AddHostedService<ServerAutoShutdownService>();
builder.Services.AddHostedService<ServerPowerOnService>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

// Редирект с корневого пути на Swagger (должен быть до MapControllers)
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.MapControllers();

// Инициализация БД и начальных данных
await InitializeDatabaseAsync(app);

app.Run();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ServerPoolDbContext>();
    context.Database.EnsureCreated();
    
    await LoadInitialDataAsync(context);
}

static async Task LoadInitialDataAsync(ServerPoolDbContext context)
{
    if (!context.Servers.Any())
    {
        
        var servers = new[]
        {
            new Server
            {
                Id = Guid.NewGuid(),
                OperatingSystem = "Windows Server 2022",
                MemoryGB = 32,
                DiskGB = 500,
                CpuCores = 8,
                Status = ServerStatus.Available
            },
            new Server
            {
                Id = Guid.NewGuid(),
                OperatingSystem = "Ubuntu 22.04",
                MemoryGB = 16,
                DiskGB = 250,
                CpuCores = 4,
                Status = ServerStatus.Available
            },
            new Server
            {
                Id = Guid.NewGuid(),
                OperatingSystem = "CentOS 8",
                MemoryGB = 64,
                DiskGB = 1000,
                CpuCores = 16,
                Status = ServerStatus.PoweringOn,
                PowerOnRequestedAt = DateTime.UtcNow
            }
        };

        context.Servers.AddRange(servers);
        await context.SaveChangesAsync();
    }
}
