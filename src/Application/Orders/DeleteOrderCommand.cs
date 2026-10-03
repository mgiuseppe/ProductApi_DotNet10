using Application.Exceptions;
using MediatR;

namespace Application.Orders;

public record DeleteOrderCommand(Guid Id) : IRequest;

public class DeleteOrderCommandHandler(IOrderRepository ordersRepository, IUnitOfWork uow) : IRequestHandler<DeleteOrderCommand>
{
    public async Task Handle(DeleteOrderCommand request, CancellationToken cancellationToken)
    {
        var o = await ordersRepository.GetByIdAsync(request.Id, cancellationToken) ?? throw new NotFoundException($"Order {request.Id} not found");

        ordersRepository.Delete(o);
        await uow.SaveChangesAsync(cancellationToken);
    }
}