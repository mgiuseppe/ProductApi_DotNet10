using Application.Exceptions;
using Application.Responses;
using MediatR;

namespace Application.Orders;

public record ConfirmOrderCommand(Guid Id) : IRequest<OrderWithLinesResponse>;

/// <summary>
/// Silently re-syncs stale line prices before confirming (Order.Draft acts as a live-priced cart).
/// See README § Order/Cart boundary for the reasoning — this is a deliberate simplification: 
/// A real e-commerce would surface price changes to the user before checkout.
/// </summary>
internal class ConfirmOrderCommandHandler(IOrderRepository ordersRepository, IUnitOfWork uow, ProductLookup productLookup) : IRequestHandler<ConfirmOrderCommand, OrderWithLinesResponse>
{
    public async Task<OrderWithLinesResponse> Handle(ConfirmOrderCommand request, CancellationToken cancellationToken)
    {
        var o = await ordersRepository.GetByIdAsync(request.Id, cancellationToken) ?? throw new NotFoundException($"Order {request.Id} not found");
        var prodsById = await productLookup.GetProductsOrThrowAsync(o.Lines.Select(l => l.ProductId), cancellationToken);

        foreach (var l in o.Lines)
        {
            var prod = prodsById[l.ProductId];
            if (prod.Price != l.UnitPriceSnapshot)
                o.UpdateLine(l.Id, prod.Price, l.Quantity);
        }

        o.Confirm();
        await uow.SaveChangesAsync(cancellationToken);

        return OrderWithLinesResponse.FromDomain(o);
    }
}