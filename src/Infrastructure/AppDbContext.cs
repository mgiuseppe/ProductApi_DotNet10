using Application.Common.Interfaces;
using Application.Common.Exceptions;
using Domain.Orders;
using Domain.Products;
using Domain.Vouchers;
using Microsoft.EntityFrameworkCore;
using Domain.Common;
using Npgsql;

namespace Infrastructure;

internal class AppDbContext : DbContext, IUnitOfWork, IQueryContext
{
    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<Voucher> Vouchers { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> opts) : base(opts)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Needed to automatically retrieve configuration classes in the same assembly
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Configure ignore for the property DomainEvents on each entity that inherit from BaseAggregateRoot
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                 .Where(t => typeof(BaseAggregateRoot).IsAssignableFrom(t.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType).Ignore(nameof(BaseAggregateRoot.DomainEvents));
        }
    }

    /// <summary>
    /// Translates unique constraint violations (e.g. the same voucher applied to two orders concurrently)
    /// into a ConflictException, so the layers above don't depend on EF/Npgsql exception types.
    /// </summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("The operation conflicts with the current state of the data", e);
        }
    }
}
