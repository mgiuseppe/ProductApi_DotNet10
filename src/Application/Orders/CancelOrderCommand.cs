using Application.Common.Interfaces;
using Application.Common.Exceptions;
using Application.Responses;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Orders;

public record CancelOrderCommand(Guid Id) : IRequest<OrderWithLinesResponse>;

public class CancelOrderCommandHandler(IOrderRepository ordersRepository, IUnitOfWork uow,
                                       IOrderHistoryLog history, TimeProvider timeProvider, 
                                       ILogger<CancelOrderCommandHandler> logger) : IRequestHandler<CancelOrderCommand, OrderWithLinesResponse>
{
    public async Task<OrderWithLinesResponse> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var o = await ordersRepository.GetByIdAsync(request.Id, cancellationToken) ?? throw new NotFoundException($"Order {request.Id} not found");

        o.Cancel();
        await uow.SaveChangesAsync(cancellationToken);

        try
        {
            await history.AppendAsync(new OrderCanceledEntry(Guid.CreateVersion7(), o.Id, timeProvider.GetUtcNow().UtcDateTime, Reason: null), CancellationToken.None); // state already committed: don't let a client disconnect drop the log
        }
        catch (Exception e)
        {
            // log registration is not critical
            logger.LogWarning(e, "Order history append failed for order {OrderId}", o.Id);
        }

        return OrderWithLinesResponse.FromDomain(o);
    }
}
