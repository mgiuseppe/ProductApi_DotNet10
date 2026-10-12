using Application.Common.Interfaces;
using Application.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Orders;

public record GetOrderByIdQuery(Guid OrderId) : IRequest<OrderWithLinesResponse?>;

public class GetOrderByIdQueryHandler(IQueryContext db) : IRequestHandler<GetOrderByIdQuery, OrderWithLinesResponse?>
{
    public async Task<OrderWithLinesResponse?> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        return order is null ? null : OrderWithLinesResponse.FromDomain(order);
    }
}
