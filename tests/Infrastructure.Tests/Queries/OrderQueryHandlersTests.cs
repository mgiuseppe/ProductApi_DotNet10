using Application.Orders;
using Domain.Orders;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Queries;

// These tests live in Infrastructure.Tests (not Application.Tests) because
// query handlers depend on the real AppDbContext (internal to Infrastructure)
// to verify EF's actual SQL translation — see README § Testing strategy.
public class OrderQueryHandlersTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public OrderQueryHandlersTests()
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

    private async Task SeedAsync(params Order[] orders)
    {
        await using var db = NewContext();
        db.Orders.AddRange(orders);
        await db.SaveChangesAsync();
    }

    // Integration Test
    [Fact]
    public async Task GetOrderById_WithExistingId_ReturnsOrderWithLines()
    {
        var keyboardId = Guid.NewGuid();
        var order = Order.Create()
                         .AddLine(keyboardId, "Keyboard", 49.90m, 2)
                         .AddLine(Guid.NewGuid(), "Mouse", 19.99m, 1);
        await SeedAsync(order);

        await using var db = NewContext(); // new context to ensure db reading
        var found = await new GetOrderByIdQueryHandler(db).Handle(new GetOrderByIdQuery(order.Id), CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(order.Id, found.Id);
        Assert.Equal(OrderStatus.Draft, found.Status);
        Assert.Equal(2, found.Lines.Count);
        Assert.Equal(119.79m, found.Total);
        var keyboardLine = Assert.Single(found.Lines, l => l.ProductId == keyboardId);
        Assert.Equal("Keyboard", keyboardLine.ProductNameSnapshot);
        Assert.Equal(49.90m, keyboardLine.UnitPriceSnapshot);
        Assert.Equal(2, keyboardLine.Quantity);
    }

    // Integration Test
    [Fact]
    public async Task GetOrderById_WithNonExistingId_ReturnsNull()
    {
        await using var db = NewContext();

        var found = await new GetOrderByIdQueryHandler(db).Handle(new GetOrderByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(found);
    }

    // Integration Test
    [Fact]
    public async Task GetOrdersList_ReturnsAllOrders_WithPaging()
    {
        var orders = new[] { Order.Create(), Order.Create(), Order.Create() };
        await SeedAsync(orders);

        await using var db = NewContext();
        var handler = new GetOrdersListQueryHandler(db);

        var firstPage = await handler.Handle(new GetOrdersListQuery(Page: 1, PageSize: 2), CancellationToken.None);
        Assert.Equal(2, firstPage.Count);

        var secondPage = await handler.Handle(new GetOrdersListQuery(Page: 2, PageSize: 2), CancellationToken.None);
        Assert.Single(secondPage);

        // Ids are random, so we only check that pages don't overlap and together cover every order
        var listedIds = firstPage.Concat(secondPage).Select(o => o.Id).ToList();
        Assert.Equal(orders.Select(o => o.Id).Order(), listedIds.Order());
    }
}
