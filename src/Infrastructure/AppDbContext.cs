using Application.Common.Interfaces;
using Application.Common.Exceptions;
using Domain.Orders;
using Domain.Products;
using Domain.Vouchers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

internal class AppDbContext : DbContext, IUnitOfWork, IQueryContext
{
    private const int SqliteConstraintUnique = 2067; // SQLITE_CONSTRAINT_UNIQUE extended error code

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
    }

    /// <summary>
    /// Translates unique constraint violations (e.g. the same voucher applied to two orders concurrently)
    /// into a ConflictException, so the layers above don't depend on EF/SQLite exception types.
    /// </summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteConstraintUnique })
        {
            throw new ConflictException("The operation conflicts with the current state of the data", e);
        }
    }
}
