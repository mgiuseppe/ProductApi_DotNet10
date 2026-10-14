using Application.Common.Interfaces;
using Application.Orders;
using Domain.Orders;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Application.Tests;

public class OrderEventHandlersTests
{
    private readonly Mock<IOrderHistoryLog> _historyLog = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
    private readonly Meter _meter = new(OrderMetrics.MeterName);

    [Fact]
    public async Task OrderConfirmed_AppendsConfirmedEntryWithEventSnapshot()
    {
        var e = new OrderConfirmedDomainEvent(Guid.NewGuid(), 15m, Guid.NewGuid());
        var appended = CaptureAppendedEntries();
        var sut = NewConfirmedHandler(new FakeLogger<OrderConfirmedEventHandler>());

        await sut.Handle(e, CancellationToken.None);

        var entry = Assert.IsType<OrderConfirmedEntry>(Assert.Single(appended));
        Assert.Equal(e.EventId, entry.Id); // event id as document id: a redelivered event is rejected, not duplicated
        Assert.Equal(e.OrderId, entry.OrderId);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, entry.OccurredAt);
        Assert.Equal(15m, entry.Total);
        Assert.Equal(e.VoucherId, entry.VoucherId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OrderConfirmed_RecordsConfirmedMetricWithVoucherTag(bool withVoucher)
    {
        var e = new OrderConfirmedDomainEvent(Guid.NewGuid(), 15m, withVoucher ? Guid.NewGuid() : null);
        using var confirmed = new MetricCollector<long>(_meter, "orders.confirmed");
        var sut = NewConfirmedHandler(new FakeLogger<OrderConfirmedEventHandler>());

        await sut.Handle(e, CancellationToken.None);

        var measurement = Assert.Single(confirmed.GetMeasurementSnapshot());
        Assert.Equal(1, measurement.Value);
        Assert.Equal(withVoucher, measurement.Tags["with_voucher"]);
    }

    [Fact]
    public async Task OrderConfirmed_WhenAppendFails_LogsErrorAndStillRecordsMetric()
    {
        var e = new OrderConfirmedDomainEvent(Guid.NewGuid(), 15m, null);
        SetupAppendFails();
        using var confirmed = new MetricCollector<long>(_meter, "orders.confirmed");
        var logger = new FakeLogger<OrderConfirmedEventHandler>();
        var sut = NewConfirmedHandler(logger);

        await sut.Handle(e, CancellationToken.None);

        Assert.Single(confirmed.GetMeasurementSnapshot());
        var log = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Error, log.Level);
        Assert.IsType<InvalidOperationException>(log.Exception);
    }

    [Fact]
    public async Task OrderConfirmed_AppendsEvenWhenRequestIsCanceled()
    {
        // Once the order is committed, a client disconnect must not drop the history entry
        var e = new OrderConfirmedDomainEvent(Guid.NewGuid(), 15m, null);
        var tokens = CaptureAppendTokens();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sut = NewConfirmedHandler(new FakeLogger<OrderConfirmedEventHandler>());

        await sut.Handle(e, cts.Token);

        var token = Assert.Single(tokens);
        Assert.False(token.IsCancellationRequested);
        Assert.True(token.CanBeCanceled); // bounded by its own timeout
    }

    [Fact]
    public async Task OrderCanceled_AppendsCanceledEntryWithEventSnapshot()
    {
        var e = new OrderCanceledDomainEvent(Guid.NewGuid());
        var appended = CaptureAppendedEntries();
        var sut = NewCanceledHandler(new FakeLogger<OrderCanceledEventHandler>());

        await sut.Handle(e, CancellationToken.None);

        var entry = Assert.IsType<OrderCanceledEntry>(Assert.Single(appended));
        Assert.Equal(e.EventId, entry.Id);
        Assert.Equal(e.OrderId, entry.OrderId);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, entry.OccurredAt);
        Assert.Null(entry.Reason);
    }

    [Fact]
    public async Task OrderCanceled_WhenAppendFails_LogsErrorWithoutThrowing()
    {
        var e = new OrderCanceledDomainEvent(Guid.NewGuid());
        SetupAppendFails();
        var logger = new FakeLogger<OrderCanceledEventHandler>();
        var sut = NewCanceledHandler(logger);

        await sut.Handle(e, CancellationToken.None);

        var log = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Error, log.Level);
        Assert.IsType<InvalidOperationException>(log.Exception);
    }

    [Fact]
    public async Task OrderCanceled_AppendsEvenWhenRequestIsCanceled()
    {
        var e = new OrderCanceledDomainEvent(Guid.NewGuid());
        var tokens = CaptureAppendTokens();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sut = NewCanceledHandler(new FakeLogger<OrderCanceledEventHandler>());

        await sut.Handle(e, cts.Token);

        var token = Assert.Single(tokens);
        Assert.False(token.IsCancellationRequested);
        Assert.True(token.CanBeCanceled);
    }

    #region private helpers
    private OrderMetrics NewOrderMetrics()
    {
        // The factory hands out _meter, so tests can attach a MetricCollector to it
        var meterFactory = new Mock<IMeterFactory>();
        meterFactory
            .Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns(_meter);
        return new OrderMetrics(meterFactory.Object);
    }

    private OrderConfirmedEventHandler NewConfirmedHandler(FakeLogger<OrderConfirmedEventHandler> logger) =>
        new(_historyLog.Object, NewOrderMetrics(), logger, _timeProvider);

    private OrderCanceledEventHandler NewCanceledHandler(FakeLogger<OrderCanceledEventHandler> logger) =>
        new(_historyLog.Object, logger, _timeProvider);

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
