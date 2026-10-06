using Domain;
using MediatR;

namespace Application.Vouchers;

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
