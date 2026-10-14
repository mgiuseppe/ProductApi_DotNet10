using Application.Common.Interfaces;
using Domain.Orders;
using MediatR;

namespace Application.Orders;

internal class OrderConfirmedEventHandler(IOrderHistoryLog history, TimeProvider time)
    : INotificationHandler<OrderConfirmedDomainEvent>
{
    public async Task Handle(OrderConfirmedDomainEvent notification, CancellationToken cancellationToken)
    {
        var entry = new OrderConfirmedEntry(
            notification.EventId,
            notification.OrderId,
            time.GetUtcNow().UtcDateTime,
            notification.Total,
            notification.VoucherId
        );

        await history.AppendAsync(entry, cancellationToken);
    }
}
