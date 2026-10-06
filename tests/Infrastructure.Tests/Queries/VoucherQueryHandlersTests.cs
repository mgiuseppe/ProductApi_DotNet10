using Application.Vouchers;
using Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Queries;

// These tests live in Infrastructure.Tests (not Application.Tests) because 
// query handlers depend on the real AppDbContext (internal to Infrastructure) 
// to verify EF's actual SQL translation — see README § Testing strategy.
public class VoucherQueryHandlersTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public VoucherQueryHandlersTests()
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

    // Integration Test
    [Fact]
    public async Task GetVoucherById_WithExistingId_ReturnsVoucher()
    {
        var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
        var canceledOn = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
        var voucher = Voucher.Create(start, end, 10.50m);
        voucher.Cancel(canceledOn);
        await using (var seed = NewContext())
        {
            seed.Vouchers.Add(voucher);
            await seed.SaveChangesAsync();
        }

        await using var db = NewContext(); // new context to ensure db reading
        var found = await new GetVoucherByIdQueryHandler(db).Handle(new GetVoucherByIdQuery(voucher.Id), CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(voucher.Id, found.Id);
        Assert.Equal(start, found.ValidityStartDate);
        Assert.Equal(end, found.ValidityEndDate);
        Assert.Equal(10.50m, found.Amount);
        Assert.Null(found.RedeemedOn);
        Assert.Equal(canceledOn, found.CanceledOn);
    }

    // Integration Test
    [Fact]
    public async Task GetVoucherById_WithNonExistingId_ReturnsNull()
    {
        await using var db = NewContext();

        var found = await new GetVoucherByIdQueryHandler(db).Handle(new GetVoucherByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(found);
    }
}
