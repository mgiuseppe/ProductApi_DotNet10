namespace Application.Orders;

/// <summary>
/// Each time you add a new order history entry type you need to register it in:
/// - src/Infrastructure/OrderHistoryEntriesConfiguration.cs (Mongo class map)
/// - src/Api/DependencyInjection.cs (JSON polymorphism)
/// Discriminators are a stored contract: NEVER CHANGE THEM!
///
/// This could be automated, but we keep it explicit for simplicity.
/// SchemaVersion is needed for futureproofeness. 
/// If you change something you'll be able to serialize/deserialize different versions with different methods  
/// </summary>
public static class OrderHistoryEntriesTypes
{
    public const string OrderConfirmedType = "OrderConfirmed";
    public const string OrderCanceledType = "OrderCanceled";
}

public abstract record OrderHistoryEntry(Guid Id, Guid OrderId, DateTime OccurredAt, int SchemaVersion);

public sealed record OrderConfirmedEntry(Guid Id, Guid OrderId, DateTime OccurredAt, decimal Total, Guid? VoucherId)
        : OrderHistoryEntry(Id, OrderId, OccurredAt, SchemaVersion: CurrentVersion)
{
    public const int CurrentVersion = 1;
}
public sealed record OrderCanceledEntry(Guid Id, Guid OrderId, DateTime OccurredAt, string? Reason)
        : OrderHistoryEntry(Id, OrderId, OccurredAt, SchemaVersion: CurrentVersion)
{
    public const int CurrentVersion = 1;
}