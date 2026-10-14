using Domain.Common;
using Domain.Orders;
using Infrastructure.Interceptors;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Infrastructure.Tests.Interceptors;

public class DispatchDomainEventsInterceptorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Mock<IMediator> _mediator = new();
    private readonly List<BaseDomainEvent> _published = [];

    public DispatchDomainEventsInterceptorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open(); // in-memory database exsists until the connection is open

        using var db = NewContext();
        db.Database.Migrate(); // create model tables

        _mediator
            .Setup(m => m.Publish(It.IsAny<BaseDomainEvent>(), It.IsAny<CancellationToken>()))
            .Callback<BaseDomainEvent, CancellationToken>((e, _) => _published.Add(e))
            .Returns(Task.CompletedTask);
    }

    public void Dispose() => _connection.Dispose();

    // Integration Test
    [Fact]
    public async Task SaveChangesAsync_PublishesEventsAndClearsThem()
    {
        var order = NewConfirmedOrder();
        var raised = order.DomainEvents.Single();

        await using (var db = NewContext())
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        Assert.Same(raised, Assert.Single(_published));
        Assert.Empty(order.DomainEvents);
    }

    // Integration Test
    [Fact]
    public void SaveChanges_PublishesEventsAndClearsThem()
    {
        var order = NewConfirmedOrder();

        using (var db = NewContext())
        {
            db.Orders.Add(order);
            db.SaveChanges();
        }

        Assert.IsType<OrderConfirmedDomainEvent>(Assert.Single(_published));
        Assert.Empty(order.DomainEvents);
    }

    // Integration Test
    [Fact]
    public async Task SaveChangesAsync_WithoutEvents_PublishesNothing()
    {
        await using (var db = NewContext())
        {
            db.Orders.Add(Order.Create().AddLine(Guid.NewGuid(), "Keyboard", 49.90m, 1));
            await db.SaveChangesAsync();
        }

        Assert.Empty(_published);
    }

    // Integration Test
    [Fact]
    public async Task SaveChangesAsync_PublishesEventsOfEveryTrackedAggregate()
    {
        var first = NewConfirmedOrder();
        var second = NewConfirmedOrder();

        await using (var db = NewContext())
        {
            db.Orders.AddRange(first, second);
            await db.SaveChangesAsync();
        }

        Assert.Equal(2, _published.Count);
        Assert.Contains(_published, e => e is OrderConfirmedDomainEvent c && c.OrderId == first.Id);
        Assert.Contains(_published, e => e is OrderConfirmedDomainEvent c && c.OrderId == second.Id);
    }

    // Integration Test
    [Fact]
    public async Task SaveChangesAsync_PublishesOnlyAfterCommit()
    {
        // The handler reads the order from a separate context: it must already be in the database
        var order = NewConfirmedOrder();
        OrderStatus? statusSeenByHandler = null;
        _mediator
            .Setup(m => m.Publish(It.IsAny<BaseDomainEvent>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await using var other = new AppDbContext(NewOptions(withInterceptor: false));
                statusSeenByHandler = (await other.Orders.SingleOrDefaultAsync(o => o.Id == order.Id))?.Status;
            });

        await using (var db = NewContext())
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        Assert.Equal(OrderStatus.Confirmed, statusSeenByHandler);
    }

    // Integration Test
    [Fact]
    public async Task SaveChangesAsync_WhenSaveFails_PublishesNothingAndKeepsEvents()
    {
        using var emptyDb = new SqliteConnection("DataSource=:memory:"); // no tables: the save fails
        emptyDb.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
                            .UseSqlite(emptyDb)
                            .AddInterceptors(NewInterceptor())
                            .Options;
        var order = NewConfirmedOrder();

        await using (var db = new AppDbContext(options))
        {
            db.Orders.Add(order);
            await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
        }

        Assert.Empty(_published);
        Assert.Single(order.DomainEvents);
    }

    // Integration Test
    [Fact]
    public async Task SaveChangesAsync_WhenPublishFails_KeepsSavedChangesAndPublishesFollowingEvents()
    {
        var order = NewConfirmedOrder();
        order.Cancel(); // two events: confirmed, then canceled
        _mediator
            .Setup(m => m.Publish(It.IsAny<BaseDomainEvent>(), It.IsAny<CancellationToken>()))
            .Returns<BaseDomainEvent, CancellationToken>((e, _) =>
            {
                if (e is OrderConfirmedDomainEvent)
                    return Task.FromException(new InvalidOperationException("Handler failed"));
                _published.Add(e);
                return Task.CompletedTask;
            });

        await using (var db = NewContext())
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync(); // does not throw
        }

        Assert.IsType<OrderCanceledDomainEvent>(Assert.Single(_published));
        Assert.Empty(order.DomainEvents);
        await using (var db = NewContext())
        {
            var saved = await db.Orders.SingleAsync(o => o.Id == order.Id);
            Assert.Equal(OrderStatus.Cancelled, saved.Status);
        }
    }

    // Integration Test
    [Fact]
    public async Task SaveChangesAsync_TwiceOnSameContext_DoesNotPublishEventsAgain()
    {
        var order = NewConfirmedOrder();

        await using (var db = NewContext())
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            await db.SaveChangesAsync();
        }

        Assert.Single(_published);
    }

    #region private helpers
    private static Order NewConfirmedOrder()
    {
        var order = Order.Create().AddLine(Guid.NewGuid(), "Keyboard", 49.90m, 1);
        order.Confirm();
        return order;
    }

    private DispatchDomainEventsInterceptor NewInterceptor() =>
        new(_mediator.Object, NullLogger<DispatchDomainEventsInterceptor>.Instance);

    private DbContextOptions<AppDbContext> NewOptions(bool withInterceptor = true)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection);
        if (withInterceptor)
            builder.AddInterceptors(NewInterceptor());
        return builder.Options;
    }

    private AppDbContext NewContext() => new(NewOptions());
    #endregion
}
