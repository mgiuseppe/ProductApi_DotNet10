using Domain.Vouchers;

namespace Application.Common.Interfaces;

public interface IVoucherRepository
{
    Task<Voucher?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Voucher voucher, CancellationToken ct);
}
