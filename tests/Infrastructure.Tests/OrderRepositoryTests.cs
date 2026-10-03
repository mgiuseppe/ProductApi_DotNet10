using Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests;

public class OrderRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public OrderRepositoryTests()
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
    public async Task Add_ThenGetById_ReturnsSavedOrderWithLines()
    {
        var keyboardId = Guid.NewGuid();
        var mouseId = Guid.NewGuid();
        var order = Order.Create()
                         .AddLine(keyboardId, "Keyboard", 49.90m, 2)
                         .AddLine(mouseId, "Mouse", 19.99m, 1);

        await using (var db = NewContext())
        {
            await new OrderRepository(db).AddAsync(order, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext()) // new context to ensure db reading
        {
            var found = await new OrderRepository(db).GetByIdAsync(order.Id, CancellationToken.None);

            Assert.NotNull(found);
            Assert.Equal(OrderStatus.Draft, found.Status);
            Assert.Equal(2, found.Lines.Count);
            Assert.Equal(119.79m, found.Total);

            var keyboardLine = Assert.Single(found.Lines, l => l.ProductId == keyboardId);
            Assert.Equal("Keyboard", keyboardLine.ProductNameSnapshot);
            Assert.Equal(49.90m, keyboardLine.UnitPriceSnapshot);
            Assert.Equal(2, keyboardLine.Quantity);
        }
    }

    // Integration Test
    [Fact]
    public async Task Update_Confirm_PersistsNewStatus()
    {
        var order = Order.Create().AddLine(Guid.NewGuid(), "Keyboard", 49.90m, 1);

        await using (var db = NewContext())
        {
            await new OrderRepository(db).AddAsync(order, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext()) // new context to load, confirm and save
        {
            var loaded = await new OrderRepository(db).GetByIdAsync(order.Id, CancellationToken.None);

            Assert.NotNull(loaded);
            loaded.Confirm();

            await db.SaveChangesAsync();
        }

        await using (var db = NewContext()) // yet another context to verify the new status
        {
            var found = await new OrderRepository(db).GetByIdAsync(order.Id, CancellationToken.None);

            Assert.NotNull(found);
            Assert.Equal(OrderStatus.Confirmed, found.Status);
        }
    }

    // Integration Test
    [Fact]
    public async Task Update_AddLineToExistingOrder_PersistsNewLine()
    {
        var order = Order.Create().AddLine(Guid.NewGuid(), "Keyboard", 49.90m, 1);

        await using (var db = NewContext())
        {
            await new OrderRepository(db).AddAsync(order, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext()) // new context to load, add a line and save
        {
            var loaded = await new OrderRepository(db).GetByIdAsync(order.Id, CancellationToken.None);

            Assert.NotNull(loaded);
            loaded.AddLine(Guid.NewGuid(), "Mouse", 19.99m, 3);

            await db.SaveChangesAsync();
        }

        await using (var db = NewContext()) // yet another context to verify the new line
        {
            var found = await new OrderRepository(db).GetByIdAsync(order.Id, CancellationToken.None);

            Assert.NotNull(found);
            Assert.Equal(2, found.Lines.Count);
            Assert.Contains(found.Lines, l => l.ProductNameSnapshot == "Mouse" && l.Quantity == 3);
        }
    }

    // Integration Test
    [Fact]
    public async Task Update_ChangeLine_PersistsNewPriceAndQuantity()
    {
        var order = Order.Create().AddLine(Guid.NewGuid(), "Keyboard", 49.90m, 1);
        var lineId = order.Lines.Single().Id;

        await using (var db = NewContext())
        {
            await new OrderRepository(db).AddAsync(order, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext()) // new context to load, update the line and save
        {
            var loaded = await new OrderRepository(db).GetByIdAsync(order.Id, CancellationToken.None);

            Assert.NotNull(loaded);
            loaded.UpdateLine(lineId, 39.90m, 4);

            await db.SaveChangesAsync();
        }

        await using (var db = NewContext()) // yet another context to verify the updated line
        {
            var found = await new OrderRepository(db).GetByIdAsync(order.Id, CancellationToken.None);

            Assert.NotNull(found);
            var line = Assert.Single(found.Lines);
            Assert.Equal(39.90m, line.UnitPriceSnapshot);
            Assert.Equal(4, line.Quantity);
        }
    }

    // Integration Test
    [Fact]
    public async Task Update_RemoveLine_DeletesLineFromDb()
    {
        var order = Order.Create()
                         .AddLine(Guid.NewGuid(), "Keyboard", 49.90m, 1)
                         .AddLine(Guid.NewGuid(), "Mouse", 19.99m, 1);
        var removedLineId = order.Lines.First(l => l.ProductNameSnapshot == "Mouse").Id;

        await using (var db = NewContext())
        {
            await new OrderRepository(db).AddAsync(order, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext()) // new context to load, remove the line and save
        {
            var loaded = await new OrderRepository(db).GetByIdAsync(order.Id, CancellationToken.None);

            Assert.NotNull(loaded);
            loaded.RemoveLine(removedLineId);

            await db.SaveChangesAsync();
        }

        await using (var db = NewContext()) // yet another context to verify the removal
        {
            var found = await new OrderRepository(db).GetByIdAsync(order.Id, CancellationToken.None);

            Assert.NotNull(found);
            var line = Assert.Single(found.Lines);
            Assert.Equal("Keyboard", line.ProductNameSnapshot);
            Assert.False(await db.Set<OrderLine>().AnyAsync(l => l.Id == removedLineId)); // orphan row must be deleted, not just detached
        }
    }

    // Integration Test
    [Fact]
    public async Task Delete_ThenGetById_ReturnsNull_AndCascadesLines()
    {
        var order = Order.Create()
                         .AddLine(Guid.NewGuid(), "Keyboard", 49.90m, 1)
                         .AddLine(Guid.NewGuid(), "Mouse", 19.99m, 1);

        await using (var db = NewContext())
        {
            var repository = new OrderRepository(db);
            await repository.AddAsync(order, CancellationToken.None);
            await db.SaveChangesAsync();

            var loaded = await repository.GetByIdAsync(order.Id, CancellationToken.None);
            Assert.NotNull(loaded);

            repository.Delete(loaded);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext()) // new context to ensure db reading
        {
            var found = await new OrderRepository(db).GetByIdAsync(order.Id, CancellationToken.None);

            Assert.Null(found);
            Assert.False(await db.Set<OrderLine>().AnyAsync());
        }
    }

    // Integration Test
    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
    {
        await using var db = NewContext();

        var found = await new OrderRepository(db).GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(found);
    }
}
