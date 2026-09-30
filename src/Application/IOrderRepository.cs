using Domain;

namespace Application;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Order>> ListOrdersAsync(int page, int pageSize, CancellationToken ct);
    Task AddAsync(Order order, CancellationToken ct);
    void Delete(Order order);
}
