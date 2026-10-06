using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Vouchers;

public record GetVoucherByIdQuery(Guid Id) : IRequest<VoucherResponse?>;

public class GetVoucherByIdQueryHandler(IQueryContext db) : IRequestHandler<GetVoucherByIdQuery, VoucherResponse?>
{
    public async Task<VoucherResponse?> Handle(GetVoucherByIdQuery request, CancellationToken cancellationToken)
    {
        var voucher = await db.Vouchers
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken);

        return voucher is null ? null : VoucherResponse.FromDomain(voucher);
    }
}
