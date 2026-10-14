using Application.Common.Interfaces;
using Application.Responses;
using Domain.Orders;
using MediatR;

namespace Application.Orders;

public record CreateOrderCommand(IReadOnlyList<CreateOrderLine> Lines) : IRequest<OrderWithLinesResponse>;
public record CreateOrderLine(Guid ProductId, int Quantity);

internal class CreateOrderCommandHandler(IOrderRepository ordersRepository,
                                       ProductLookup productLookup,
                                       IUnitOfWork uow) : IRequestHandler<CreateOrderCommand, OrderWithLinesResponse>
{
    public async Task<OrderWithLinesResponse> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var prodsById = await productLookup.GetProductsOrThrowAsync(request.Lines.Select(l => l.ProductId).Distinct(), cancellationToken);

        var o = Order.Create();
        foreach (var l in request.Lines)
        {
            var prod = prodsById[l.ProductId];
            o.AddLine(l.ProductId, prod.Name, prod.Price, l.Quantity);
        }

        await ordersRepository.AddAsync(o, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);

        return OrderWithLinesResponse.FromDomain(o);
    }
}
