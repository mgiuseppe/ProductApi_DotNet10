using Application;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

internal class AppDbContext : DbContext, IUnitOfWork
{
    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> opts) : base(opts)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Needed to automatically retrieve configuration classes in the same assembly
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
