using Application.Common.Interfaces;
using Domain.Vouchers;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

internal class VoucherRepository(AppDbContext dbContext) : IVoucherRepository
{
    private readonly AppDbContext _db = dbContext;

    public async Task<Voucher?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _db.Vouchers.FirstOrDefaultAsync(v => v.Id == id, cancellationToken: ct);
    }

    public async Task AddAsync(Voucher voucher, CancellationToken ct)
    {
        await _db.Vouchers.AddAsync(voucher, ct);
    }
}
