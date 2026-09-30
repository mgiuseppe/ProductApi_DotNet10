using Application.Commands;
using Domain;

namespace Application;

public interface IOrderService
{
    Task<Order> CreateAsync(CreateOrderCommand cmd, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Order>> GetListAsync(int page, int pageSize, CancellationToken ct);
    Task<Order> UpdateAsync(Guid id, UpdateOrderCommand cmd, CancellationToken ct);
    
    Task<Order> ConfirmAsync(Guid id, CancellationToken ct);
    
    Task<Order> ShipAsync(Guid id, CancellationToken ct);
    
    Task<Order> CancelAsync(Guid id, CancellationToken ct);
}
