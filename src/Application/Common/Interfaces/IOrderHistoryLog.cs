
using Application.Orders;

namespace Application.Common.Interfaces;

public interface IOrderHistoryLog
{
    Task AppendAsync(OrderHistoryEntry entry, CancellationToken ct);
    Task<IReadOnlyList<OrderHistoryEntry>> GetByOrderIdAsync(Guid orderId, CancellationToken ct);
}