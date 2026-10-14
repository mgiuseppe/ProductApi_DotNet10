using Domain.Common;
using Domain.Orders;
using Infrastructure.Interceptors;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
    public async Task SaveChangesAsync_PublishesBeforeCommit()
    {
        // The handler reads the order from a separate context: it must not be in the database yet
        var order = NewConfirmedOrder();
        var handlerRan = false;
        Order? seenByHandler = null;
        _mediator
            .Setup(m => m.Publish(It.IsAny<BaseDomainEvent>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                handlerRan = true;
                await using var other = new AppDbContext(NewOptions(withInterceptor: false));
                seenByHandler = await other.Orders.SingleOrDefaultAsync(o => o.Id == order.Id);
            });

        await using (var db = NewContext())
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        Assert.True(handlerRan);
        Assert.Null(seenByHandler);
    }

    // Integration Test
    [Fact]
    public async Task SaveChangesAsync_SavesChangesMadeByHandlersInTheSameCommit()
    {
        // DetectChanges runs after SavingChanges, so entities added by a handler are saved too
        var order = NewConfirmedOrder();
        var addedByHandler = Order.Create().AddLine(Guid.NewGuid(), "Mouse", 19.90m, 1);
        await using (var db = NewContext())
        {
            _mediator
                .Setup(m => m.Publish(It.IsAny<BaseDomainEvent>(), It.IsAny<CancellationToken>()))
                .Callback(() => db.Orders.Add(addedByHandler))
                .Returns(Task.CompletedTask);

            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            Assert.True(await db.Orders.AnyAsync(o => o.Id == order.Id));
            Assert.True(await db.Orders.AnyAsync(o => o.Id == addedByHandler.Id));
        }
    }

    // Integration Test
    [Fact]
    public async Task SaveChangesAsync_DoesNotPublishEventsRaisedByHandlers()
    {
        var order = NewConfirmedOrder();
        var addedByHandler = NewConfirmedOrder();
        await using (var db = NewContext())
        {
            _mediator
                .Setup(m => m.Publish(It.IsAny<BaseDomainEvent>(), It.IsAny<CancellationToken>()))
                .Callback<BaseDomainEvent, CancellationToken>((e, _) =>
                {
                    _published.Add(e);
                    db.Orders.Add(addedByHandler);
                })
                .Returns(Task.CompletedTask);

            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        var published = Assert.IsType<OrderConfirmedDomainEvent>(Assert.Single(_published));
        Assert.Equal(order.Id, published.OrderId);
        Assert.Single(addedByHandler.DomainEvents); // collected before the handler ran: never dispatched
    }

    // Integration Test
    [Fact]
    public async Task SaveChangesAsync_WhenSaveFails_EventsAreAlreadyPublished()
    {
        // Published before the commit: a failed save does not undo what the handlers did
        using var emptyDb = new SqliteConnection("DataSource=:memory:"); // no tables: the save fails
        emptyDb.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
                            .UseSqlite(emptyDb)
                            .AddInterceptors(NewInterceptor())
                            .Options;
        var order = NewConfirmedOrder();
        var raised = order.DomainEvents.Single();

        await using (var db = new AppDbContext(options))
        {
            db.Orders.Add(order);
            await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
        }

        Assert.Same(raised, Assert.Single(_published));
    }

    // Integration Test
    [Fact]
    public async Task SaveChangesAsync_WhenPublishFails_ThrowsAndSavesNothing()
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
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }

        Assert.Empty(_published); // sequential publisher: the following handlers do not run
        await using (var db = NewContext())
        {
            Assert.False(await db.Orders.AnyAsync(o => o.Id == order.Id));
        }
    }

    // Integration Test
    [Fact]
    public void SaveChanges_WhenPublishFails_ThrowsAndSavesNothing()
    {
        var order = NewConfirmedOrder();
        _mediator
            .Setup(m => m.Publish(It.IsAny<BaseDomainEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Handler failed"));

        using (var db = NewContext())
        {
            db.Orders.Add(order);
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        }

        using (var db = NewContext())
        {
            Assert.False(db.Orders.Any(o => o.Id == order.Id));
        }
    }

    // Integration Test
    [Fact]
    public async Task SaveChangesAsync_PassesCancellationTokenToPublish()
    {
        var tokens = new List<CancellationToken>();
        _mediator
            .Setup(m => m.Publish(It.IsAny<BaseDomainEvent>(), It.IsAny<CancellationToken>()))
            .Callback<BaseDomainEvent, CancellationToken>((_, ct) => tokens.Add(ct))
            .Returns(Task.CompletedTask);
        using var cts = new CancellationTokenSource();

        await using (var db = NewContext())
        {
            db.Orders.Add(NewConfirmedOrder());
            await db.SaveChangesAsync(cts.Token);
        }

        Assert.Equal(cts.Token, Assert.Single(tokens));
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

    private DispatchDomainEventsInterceptor NewInterceptor() => new(_mediator.Object);

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
