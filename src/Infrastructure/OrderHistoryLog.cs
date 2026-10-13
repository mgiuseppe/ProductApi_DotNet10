using Application.Common.Interfaces;
using Application.Orders;
using MongoDB.Driver;

namespace Infrastructure;

internal class OrderHistoryLog(IMongoDatabase database) : IOrderHistoryLog
{
    public const string CollectionName = "order_history";

    private readonly IMongoCollection<OrderHistoryEntry> _history = database.GetCollection<OrderHistoryEntry>(CollectionName);

    public async Task AppendAsync(OrderHistoryEntry entry, CancellationToken ct)
    {
        await _history.InsertOneAsync(entry, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<OrderHistoryEntry>> GetByOrderIdAsync(Guid orderId, CancellationToken ct)
    {
        return await _history.Find(e => e.OrderId == orderId)
                            .SortBy(e => e.OccurredAt)
                            .ToListAsync(ct);
    }
}