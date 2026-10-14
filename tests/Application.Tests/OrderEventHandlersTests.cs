using Application.Common.Interfaces;
using Application.Orders;
using Domain.Orders;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Application.Tests;

public class OrderEventHandlersTests
{
    private readonly Mock<IOrderHistoryLog> _historyLog = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task OrderConfirmed_AppendsConfirmedEntryWithEventSnapshot()
    {
        var e = new OrderConfirmedDomainEvent(Guid.NewGuid(), 15m, Guid.NewGuid());
        var appended = CaptureAppendedEntries();
        var sut = NewConfirmedHandler();

        await sut.Handle(e, CancellationToken.None);

        var entry = Assert.IsType<OrderConfirmedEntry>(Assert.Single(appended));
        Assert.Equal(e.EventId, entry.Id); // event id as document id: a redelivered event is rejected, not duplicated
        Assert.Equal(e.OrderId, entry.OrderId);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, entry.OccurredAt);
        Assert.Equal(15m, entry.Total);
        Assert.Equal(e.VoucherId, entry.VoucherId);
    }

    [Fact]
    public async Task OrderConfirmed_WhenAppendFails_Throws()
    {
        // Handled before the commit: the exception must reach SaveChanges so the order is not saved
        var e = new OrderConfirmedDomainEvent(Guid.NewGuid(), 15m, null);
        SetupAppendFails();
        var sut = NewConfirmedHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Handle(e, CancellationToken.None));
    }

    [Fact]
    public async Task OrderConfirmed_PassesRequestCancellationTokenToAppend()
    {
        var e = new OrderConfirmedDomainEvent(Guid.NewGuid(), 15m, null);
        var tokens = CaptureAppendTokens();
        using var cts = new CancellationTokenSource();
        var sut = NewConfirmedHandler();

        await sut.Handle(e, cts.Token);

        Assert.Equal(cts.Token, Assert.Single(tokens));
    }

    [Fact]
    public async Task OrderCanceled_AppendsCanceledEntryWithEventSnapshot()
    {
        var e = new OrderCanceledDomainEvent(Guid.NewGuid());
        var appended = CaptureAppendedEntries();
        var sut = NewCanceledHandler();

        await sut.Handle(e, CancellationToken.None);

        var entry = Assert.IsType<OrderCanceledEntry>(Assert.Single(appended));
        Assert.Equal(e.EventId, entry.Id);
        Assert.Equal(e.OrderId, entry.OrderId);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, entry.OccurredAt);
        Assert.Null(entry.Reason);
    }

    [Fact]
    public async Task OrderCanceled_WhenAppendFails_Throws()
    {
        var e = new OrderCanceledDomainEvent(Guid.NewGuid());
        SetupAppendFails();
        var sut = NewCanceledHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Handle(e, CancellationToken.None));
    }

    [Fact]
    public async Task OrderCanceled_PassesRequestCancellationTokenToAppend()
    {
        var e = new OrderCanceledDomainEvent(Guid.NewGuid());
        var tokens = CaptureAppendTokens();
        using var cts = new CancellationTokenSource();
        var sut = NewCanceledHandler();

        await sut.Handle(e, cts.Token);

        Assert.Equal(cts.Token, Assert.Single(tokens));
    }

    #region private helpers
    private OrderConfirmedEventHandler NewConfirmedHandler() =>
        new(_historyLog.Object, _timeProvider);

    private OrderCanceledEventHandler NewCanceledHandler() =>
        new(_historyLog.Object, _timeProvider);

    private List<OrderHistoryEntry> CaptureAppendedEntries()
    {
        var appended = new List<OrderHistoryEntry>();
        _historyLog
            .Setup(l => l.AppendAsync(It.IsAny<OrderHistoryEntry>(), It.IsAny<CancellationToken>()))
            .Callback<OrderHistoryEntry, CancellationToken>((e, _) => appended.Add(e))
            .Returns(Task.CompletedTask);
        return appended;
    }

    private List<CancellationToken> CaptureAppendTokens()
    {
        var tokens = new List<CancellationToken>();
        _historyLog
            .Setup(l => l.AppendAsync(It.IsAny<OrderHistoryEntry>(), It.IsAny<CancellationToken>()))
            .Callback<OrderHistoryEntry, CancellationToken>((_, ct) => tokens.Add(ct))
            .Returns(Task.CompletedTask);
        return tokens;
    }

    private void SetupAppendFails()
    {
        _historyLog
            .Setup(l => l.AppendAsync(It.IsAny<OrderHistoryEntry>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Order history unavailable"));
    }
    #endregion
}
