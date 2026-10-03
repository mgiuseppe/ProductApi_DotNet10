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

    public async Task AddAsync(Order order, CancellationToken ct)
    {
        await _db.Orders.AddAsync(order, ct);
    }

    public void Delete(Order order)
    {
        _db.Orders.Remove(order);
    }
}
