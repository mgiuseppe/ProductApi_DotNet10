using Application;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

internal class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _db;

    public OrderRepository(AppDbContext dbContext)
    {
        _db = dbContext;
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _db.Orders.Include(o => o.Lines)
                               .FirstOrDefaultAsync(o => o.Id == id, cancellationToken: ct);
    }

    // TODO: this must be moved outside the repository because it's not meant to return the aggregate
    public async Task<IReadOnlyList<Order>> ListOrdersAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = _db.Orders.AsNoTracking()
                               .OrderBy(o => o.Id)
                               .Skip((page-1)*pageSize)
                               .Take(pageSize);
                               // .Include(o => o.Lines);
        
        return await query.ToListAsync(ct);
    }

    public async Task AddAsync(Order order, CancellationToken ct)
    {
        await _db.Orders.AddAsync(order, ct);
    }

    public void Delete(Order order)
    {
        _db.Orders.Remove(order);
    }
}
