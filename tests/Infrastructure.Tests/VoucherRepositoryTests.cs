using Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests;

public class VoucherRepositoryTests : IDisposable
{
    private static readonly DateTime Start = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Within = new(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public VoucherRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open(); // in-memory database exsists until the connection is open

        _options = new DbContextOptionsBuilder<AppDbContext>()
                                        .UseSqlite(_connection)
                                        .Options;

        using var db = new AppDbContext(_options);
        db.Database.Migrate(); // create model tables
    }

    private AppDbContext NewContext() => new(_options);
    public void Dispose() => _connection.Dispose();

    private async Task SeedAsync(params object[] entities)
    {
        await using var db = NewContext();
        db.AddRange(entities);
        await db.SaveChangesAsync();
    }

    // Integration Test
    [Fact]
    public async Task Add_ThenGetById_ReturnsSavedVoucher()
    {
        var voucher = Voucher.Create(Start, End, 10.50m);

        await using (var db = NewContext())
        {
            await new VoucherRepository(db).AddAsync(voucher, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext()) // new context to ensure db reading
        {
            var found = await new VoucherRepository(db).GetByIdAsync(voucher.Id, CancellationToken.None);

            Assert.NotNull(found);
            Assert.Equal(Start, found.ValidityStartDate);
            Assert.Equal(End, found.ValidityEndDate);
            Assert.Equal(10.50m, found.Amount);
            Assert.Null(found.RedeemedOn);
            Assert.Null(found.CanceledOn);
        }
    }

    // Integration Test
    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
    {
        await using var db = NewContext();

        var found = await new VoucherRepository(db).GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(found);
    }

    // Integration Test
    [Fact]
    public async Task Update_Redeem_PersistsRedeemedOn()
    {
        var voucher = Voucher.Create(Start, End, 10m);
        await SeedAsync(voucher);

        await using (var db = NewContext()) // new context to load, redeem and save
        {
            var loaded = await new VoucherRepository(db).GetByIdAsync(voucher.Id, CancellationToken.None);

            Assert.NotNull(loaded);
            loaded.Redeem(Within);

            await db.SaveChangesAsync();
        }

        await using (var db = NewContext()) // yet another context to verify the redemption
        {
            var found = await new VoucherRepository(db).GetByIdAsync(voucher.Id, CancellationToken.None);

            Assert.NotNull(found);
            Assert.Equal(Within, found.RedeemedOn);
        }
    }

    // Integration Test
    [Fact]
    public async Task Update_Cancel_PersistsCanceledOn()
    {
        var voucher = Voucher.Create(Start, End, 10m);
        await SeedAsync(voucher);

        await using (var db = NewContext()) // new context to load, cancel and save
        {
            var loaded = await new VoucherRepository(db).GetByIdAsync(voucher.Id, CancellationToken.None);

            Assert.NotNull(loaded);
            loaded.Cancel(Within);

            await db.SaveChangesAsync();
        }

        await using (var db = NewContext()) // yet another context to verify the cancellation
        {
            var found = await new VoucherRepository(db).GetByIdAsync(voucher.Id, CancellationToken.None);

            Assert.NotNull(found);
            Assert.Equal(Within, found.CanceledOn);
        }
    }

    // Integration Test
    [Fact]
    public async Task Delete_VoucherAppliedToOrder_IsRestrictedByForeignKey()
    {
        var voucher = Voucher.Create(Start, End, 5m);
        var order = Order.Create().AddLine(Guid.NewGuid(), "Keyboard", 49.90m, 1);
        new VoucherRedemptionService().ConfirmWithVoucher(order, voucher, Within);
        await SeedAsync(voucher, order);

        await using var db = NewContext();
        var loaded = await new VoucherRepository(db).GetByIdAsync(voucher.Id, CancellationToken.None);
        db.Vouchers.Remove(loaded!);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
