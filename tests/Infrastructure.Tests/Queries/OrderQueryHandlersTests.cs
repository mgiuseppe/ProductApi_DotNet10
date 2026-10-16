using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Orders;
using Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Moq;

namespace Infrastructure.Tests.Queries;

// These tests live in Infrastructure.Tests (not Application.Tests) because
// query handlers depend on the real AppDbContext (internal to Infrastructure)
// to verify EF's actual SQL translation — see README § Testing strategy.
public class OrderQueryHandlersTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private DbContextOptions<AppDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync(); // own database, migrations already applied
        _options = _database.Options();
    }

    private readonly Mock<IOrderHistoryLog> _history = new();
    private readonly Mock<IChatClient> _chat = new();

    private AppDbContext NewContext() => new(_options);
    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

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

    // Integration Test
    [Fact]
    public async Task SummarizeOrder_WithExistingId_SendsOrderAndHistoryToChatClient()
    {
        var order = Order.Create().AddLine(Guid.NewGuid(), "Keyboard", 49.90m, 2);
        await SeedAsync(order);
        var confirmed = new OrderConfirmedEntry(Guid.NewGuid(), order.Id, new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc), 99.80m, null);
        _history.Setup(h => h.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync([confirmed]);
        List<ChatMessage>? sent = null;
        ChatOptions? sentOptions = null;
        _chat.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
             .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((m, o, _) => (sent, sentOptions) = (m.ToList(), o))
             .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "The summary")));

        await using var db = NewContext();
        var result = await new SummarizeOrderQueryHandler(db, _history.Object, _chat.Object)
                               .Handle(new SummarizeOrderQuery(order.Id), CancellationToken.None);

        Assert.Equal("The summary", result);
        Assert.NotNull(sent);
        Assert.Collection(sent,
            m => Assert.Equal(ChatRole.System, m.Role),
            m => Assert.Equal(ChatRole.User, m.Role));
        var userPrompt = sent[1].Text;
        Assert.Contains(order.Id.ToString(), userPrompt);
        Assert.Contains("Keyboard", userPrompt);
        Assert.Contains("\"Draft\"", userPrompt); // enums serialized as names, not numbers
        Assert.Contains(nameof(OrderConfirmedEntry), userPrompt); // entry type tells the model what happened
        Assert.Contains(confirmed.Id.ToString(), userPrompt);
        Assert.NotNull(sentOptions);
        Assert.Equal(500, sentOptions.MaxOutputTokens);
    }

    // Integration Test
    [Fact]
    public async Task SummarizeOrder_WithNonExistingId_ThrowsNotFound()
    {
        await using var db = NewContext();
        var sut = new SummarizeOrderQueryHandler(db, _history.Object, _chat.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sut.Handle(new SummarizeOrderQuery(Guid.NewGuid()), CancellationToken.None));

        _chat.Verify(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
