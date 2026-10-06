using Domain;

namespace Application;

public interface IVoucherRepository
{
    Task<Voucher?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Voucher voucher, CancellationToken ct);
}
