using Application.Exceptions;
using Application.Responses;
using MediatR;

namespace Application.Orders;

public record CancelOrderCommand(Guid Id) : IRequest<OrderWithLinesResponse>;

public class CancelOrderCommandHandler(IOrderRepository ordersRepository, IUnitOfWork uow) : IRequestHandler<CancelOrderCommand, OrderWithLinesResponse>
{
    public async Task<OrderWithLinesResponse> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var o = await ordersRepository.GetByIdAsync(request.Id, cancellationToken) ?? throw new NotFoundException($"Order {request.Id} not found");

        o.Cancel();
        await uow.SaveChangesAsync(cancellationToken);

        return OrderWithLinesResponse.FromDomain(o);
    }
}
