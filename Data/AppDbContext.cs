using Microsoft.EntityFrameworkCore;
using ApiMocker.Models;

namespace ApiMocker.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<RouteConfig> RouteConfigs => Set<RouteConfig>();
    public DbSet<RequestLog> RequestLogs => Set<RequestLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RouteConfig>()
            .HasMany(r => r.RequestLogs)
            .WithOne(l => l.RouteConfig)
            .HasForeignKey(l => l.RouteConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique only among exact (non-template) routes
        modelBuilder.Entity<RouteConfig>()
            .HasIndex(r => new { r.Path, r.HttpMethod, r.IsTemplate });
    }
}
