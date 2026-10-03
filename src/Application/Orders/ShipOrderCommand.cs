using Application.Exceptions;
using Application.Responses;
using MediatR;

namespace Application.Orders;

public record ShipOrderCommand(Guid Id) : IRequest<OrderWithLinesResponse>;

public class ShipOrderCommandHandler(IOrderRepository ordersRepository, IUnitOfWork uow) : IRequestHandler<ShipOrderCommand, OrderWithLinesResponse>
{
    public async Task<OrderWithLinesResponse> Handle(ShipOrderCommand request, CancellationToken cancellationToken)
    {
        var o = await ordersRepository.GetByIdAsync(request.Id, cancellationToken) ?? throw new NotFoundException($"Order {request.Id} not found");

        o.Ship();
        await uow.SaveChangesAsync(cancellationToken);

        return OrderWithLinesResponse.FromDomain(o);
    }
}

