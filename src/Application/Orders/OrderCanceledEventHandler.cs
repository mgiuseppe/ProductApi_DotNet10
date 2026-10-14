using Application.Common.Interfaces;
using Domain.Orders;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Orders;

internal class OrderCanceledEventHandler(IOrderHistoryLog history, ILogger<OrderCanceledEventHandler> logger, TimeProvider time)
    : INotificationHandler<OrderCanceledDomainEvent>
{
    public async Task Handle(OrderCanceledDomainEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var entry = new OrderCanceledEntry(
                notification.EventId,
                notification.OrderId,
                time.GetUtcNow().UtcDateTime,
                Reason: null
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
