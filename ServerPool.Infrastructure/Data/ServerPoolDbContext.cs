using Microsoft.EntityFrameworkCore;
using ServerPool.Core.Models;

namespace ServerPool.Infrastructure.Data;

public class ServerPoolDbContext : DbContext
{
    public ServerPoolDbContext(DbContextOptions<ServerPoolDbContext> options)
        : base(options)
    {
    }

    public DbSet<Server> Servers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Server>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OperatingSystem).IsRequired().HasMaxLength(200);
            entity.Property(e => e.MemoryGB).IsRequired();
            entity.Property(e => e.DiskGB).IsRequired();
            entity.Property(e => e.CpuCores).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.AllocatedTo).HasMaxLength(200);
        });
    }
}
