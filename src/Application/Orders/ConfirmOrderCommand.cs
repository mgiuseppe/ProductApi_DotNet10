using Application.Common.Interfaces;
using Application.Common.Exceptions;
using Application.Responses;
using Domain.Vouchers;
using MediatR;

namespace Application.Orders;

public record ConfirmOrderCommand(Guid Id, Guid? VoucherId) : IRequest<OrderWithLinesResponse>;

/// <summary>
/// Silently re-syncs stale line prices before confirming (Order.Draft acts as a live-priced cart).
/// See README § Order/Cart boundary for the reasoning — this is a deliberate simplification: 
/// A real e-commerce would surface price changes to the user before checkout.
/// </summary>
internal class ConfirmOrderCommandHandler(IOrderRepository ordersRepository, 
                                          IVoucherRepository vouchers,
                                          VoucherRedemptionService redemption,
                                          IUnitOfWork uow, 
                                          ProductLookup productLookup,
                                          TimeProvider timeProvider) : IRequestHandler<ConfirmOrderCommand, OrderWithLinesResponse>
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

        if (request.VoucherId is { } voucherId)
        {
            var voucher = await vouchers.GetByIdAsync(voucherId, cancellationToken) ?? throw new NotFoundException($"Voucher {voucherId} not found");
            redemption.ConfirmWithVoucher(o, voucher, timeProvider.GetUtcNow().UtcDateTime);
        }
        else
        {
            o.Confirm();
        }

        await uow.SaveChangesAsync(cancellationToken);

        return OrderWithLinesResponse.FromDomain(o);
    }
}