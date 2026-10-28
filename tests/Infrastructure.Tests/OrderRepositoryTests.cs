using Application.Common.Exceptions;
using Domain;
using Domain.Orders;
using Domain.Vouchers;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests;

public class OrderRepositoryTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private DbContextOptions<AppDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync(); // own database, migrations already applied
        _options = _database.Options();
    }

    private AppDbContext NewContext() => new(_options);
    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

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
    public async Task Update_ConfirmWithVoucher_PersistsVoucherIdAndDiscount()
    {
        var now = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc); // fixed: a deterministic test does not depend on the clock
        var voucher = Voucher.Create(now.AddDays(-1), now.AddDays(1), 9.90m);
        var order = Order.Create().AddLine(Guid.NewGuid(), "Keyboard", 49.90m, 1);

        await using (var db = NewContext())
        {
            await new VoucherRepository(db).AddAsync(voucher, CancellationToken.None);
            await new OrderRepository(db).AddAsync(order, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext()) // new context to load, confirm with voucher and save
        {
            var loadedOrder = await new OrderRepository(db).GetByIdAsync(order.Id, CancellationToken.None);
            var loadedVoucher = await new VoucherRepository(db).GetByIdAsync(voucher.Id, CancellationToken.None);

            Assert.NotNull(loadedOrder);
            Assert.NotNull(loadedVoucher);
            new VoucherRedemptionService().ConfirmWithVoucher(loadedOrder, loadedVoucher, now);

            await db.SaveChangesAsync();
        }

        await using (var db = NewContext()) // yet another context to verify both aggregates
        {
            var found = await new OrderRepository(db).GetByIdAsync(order.Id, CancellationToken.None);
            var foundVoucher = await new VoucherRepository(db).GetByIdAsync(voucher.Id, CancellationToken.None);

            Assert.NotNull(found);
            Assert.Equal(OrderStatus.Confirmed, found.Status);
            Assert.Equal(voucher.Id, found.VoucherId);
            Assert.Equal(9.90m, found.DiscountAmount);
            Assert.Equal(40m, found.Total);
            Assert.NotNull(foundVoucher);
            Assert.Equal(now, foundVoucher.RedeemedOn);
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

    // Integration Test
    [Fact]
    public async Task Save_SameVoucherConfirmedConcurrentlyOnTwoOrders_SecondThrowsConflictException()
    {
        var now = DateTime.UtcNow;
        var voucher = Voucher.Create(now.AddDays(-1), now.AddDays(1), 5m);
        var orderA = Order.Create().AddLine(Guid.NewGuid(), "Keyboard", 49.90m, 1);
        var orderB = Order.Create().AddLine(Guid.NewGuid(), "Mouse", 19.99m, 1);
        await using (var db = NewContext())
        {
            await new VoucherRepository(db).AddAsync(voucher, CancellationToken.None);
            await new OrderRepository(db).AddAsync(orderA, CancellationToken.None);
            await new OrderRepository(db).AddAsync(orderB, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        // Both requests load the voucher before either saves, so the in-memory "already redeemed" check passes twice
        await using var dbA = NewContext();
        await using var dbB = NewContext();
        var redemption = new VoucherRedemptionService();
        redemption.ConfirmWithVoucher((await new OrderRepository(dbA).GetByIdAsync(orderA.Id, CancellationToken.None))!,
                                      (await new VoucherRepository(dbA).GetByIdAsync(voucher.Id, CancellationToken.None))!,
                                      now);
        redemption.ConfirmWithVoucher((await new OrderRepository(dbB).GetByIdAsync(orderB.Id, CancellationToken.None))!,
                                      (await new VoucherRepository(dbB).GetByIdAsync(voucher.Id, CancellationToken.None))!,
                                      now);

        await dbA.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() => dbB.SaveChangesAsync());
    }
}
