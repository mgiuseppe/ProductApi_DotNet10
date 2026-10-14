using Application.Common.Interfaces;
using Domain.Orders;
using MediatR;

namespace Application.Orders;

internal class OrderCanceledEventHandler(IOrderHistoryLog history, TimeProvider time)
    : INotificationHandler<OrderCanceledDomainEvent>
{
    public async Task Handle(OrderCanceledDomainEvent notification, CancellationToken cancellationToken)
    {
        var entry = new OrderCanceledEntry(
            notification.EventId,
            notification.OrderId,
            time.GetUtcNow().UtcDateTime,
            Reason: null
        );

        await history.AppendAsync(entry, cancellationToken);
    }
}
