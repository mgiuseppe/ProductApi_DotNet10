using Application.Common.Interfaces;
using MediatR;

namespace Application.Orders;

public record GetOrderHistoryQuery(Guid OrderId) : IRequest<IReadOnlyList<OrderHistoryEntry>>;

internal class GetOrderHistoryQueryHandler(IOrderHistoryLog history) : IRequestHandler<GetOrderHistoryQuery, IReadOnlyList<OrderHistoryEntry>>
{
    public async Task<IReadOnlyList<OrderHistoryEntry>> Handle(GetOrderHistoryQuery request, CancellationToken cancellationToken)
    {
        return await history.GetByOrderIdAsync(request.OrderId, cancellationToken);
    }
}