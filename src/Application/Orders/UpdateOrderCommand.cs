using Application.Exceptions;
using Application.Responses;
using MediatR;

namespace Application.Orders;

public record UpdateOrderCommand(Guid Id,
                                 IReadOnlyList<CreateOrderLine> LinesToAdd, 
                                 IReadOnlyList<UpdateOrderLine> LinesToUpdate, 
                                 IReadOnlyList<Guid> LineIdsToDelete) : IRequest<OrderWithLinesResponse>;
public record UpdateOrderLine(Guid Id, int Quantity);

internal class UpdateOrderCommandHandler(IOrderRepository ordersRepository,
                                       ProductLookup productLookup,
                                       IUnitOfWork uow) : IRequestHandler<UpdateOrderCommand, OrderWithLinesResponse>
{
    public async Task<OrderWithLinesResponse> Handle(UpdateOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await ordersRepository.GetByIdAsync(request.Id, cancellationToken) ?? throw new NotFoundException($"Order {request.Id} not found");
        var prodsById = await productLookup.GetProductsOrThrowAsync(request.LinesToAdd.Select(l => l.ProductId)
                                                                                      .Union(order.Lines.Select(l => l.ProductId))
                                                                                      .Distinct(), cancellationToken);
    
        foreach (var l in request.LinesToAdd)
        {
            var prod = prodsById[l.ProductId];
            order.AddLine(l.ProductId, prod.Name, prod.Price, l.Quantity);
        }
        foreach (var l in request.LinesToUpdate)
        {
            var existingLine = order.Lines.FirstOrDefault(line => line.Id == l.Id) ?? throw new NotFoundException($"Line {l.Id} not found");
            var prod = prodsById[existingLine.ProductId];
            order.UpdateLine(l.Id, prod.Price, l.Quantity);
        }
        foreach (var lineId in request.LineIdsToDelete)
        {
            order.RemoveLine(lineId);
        }

        await uow.SaveChangesAsync(cancellationToken);

        return OrderWithLinesResponse.FromDomain(order);
    }
}