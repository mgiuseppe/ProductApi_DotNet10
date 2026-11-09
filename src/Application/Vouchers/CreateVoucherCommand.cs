using Application.Common.Interfaces;
using Domain.Vouchers;
using MediatR;

namespace Application.Vouchers;

/// <param name="ValidityStartDate">First moment the voucher can be redeemed, included; converted to UTC.</param>
/// <param name="ValidityEndDate">Last moment the voucher can be redeemed, included; converted to UTC. Must be after the start.</param>
/// <param name="Amount">Fixed discount subtracted from the order subtotal, not a percentage. Greater than 0.</param>
public record CreateVoucherCommand(DateTime ValidityStartDate, DateTime ValidityEndDate, decimal Amount) : IRequest<VoucherResponse>;

internal class CreateVoucherCommandHandler(IVoucherRepository vouchers, IUnitOfWork uow) : IRequestHandler<CreateVoucherCommand, VoucherResponse>
{
    public async Task<VoucherResponse> Handle(CreateVoucherCommand request, CancellationToken cancellationToken)
    {
        var voucher = Voucher.Create(request.ValidityStartDate, request.ValidityEndDate, request.Amount);
        await vouchers.AddAsync(voucher, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);

        return VoucherResponse.FromDomain(voucher);
    }
}
