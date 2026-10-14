using Application.Common.Interfaces;
using Domain.Orders;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Orders;

internal class OrderConfirmedEventHandler(IOrderHistoryLog history, OrderMetrics metrics, ILogger<OrderConfirmedEventHandler> logger, TimeProvider time)
    : INotificationHandler<OrderConfirmedDomainEvent>
{
    public async Task Handle(OrderConfirmedDomainEvent notification, CancellationToken cancellationToken)
    {
        metrics.OrderConfirmed(notification.VoucherId != null);

        try
        {
            var entry = new OrderConfirmedEntry(
                notification.EventId,
                notification.OrderId,
                time.GetUtcNow().UtcDateTime,
                notification.Total,
                notification.VoucherId
            );

            // state already committed: don't let a client disconnect drop the history update
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await history.AppendAsync(entry, timeout.Token);
        }
        catch (Exception e) // history registration is not critical, so we can ignore if throws
        {
            logger.LogError(e, "Order history append failed for order {OrderId}", notification.OrderId);
        }
    }
}
