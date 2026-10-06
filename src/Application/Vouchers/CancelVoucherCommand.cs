using Application.Exceptions;
using MediatR;

namespace Application.Vouchers;

public record CancelVoucherCommand(Guid Id) : IRequest<VoucherResponse>;

internal class CancelVoucherCommandHandler(IVoucherRepository vouchers, IUnitOfWork uow, TimeProvider timeProvider) : IRequestHandler<CancelVoucherCommand, VoucherResponse>
{
    public async Task<VoucherResponse> Handle(CancelVoucherCommand request, CancellationToken cancellationToken)
    {
        var voucher = await vouchers.GetByIdAsync(request.Id, cancellationToken) ?? throw new NotFoundException($"Voucher {request.Id} not found");

        voucher.Cancel(timeProvider.GetUtcNow().UtcDateTime);
        await uow.SaveChangesAsync(cancellationToken);

        return VoucherResponse.FromDomain(voucher);
    }
}
