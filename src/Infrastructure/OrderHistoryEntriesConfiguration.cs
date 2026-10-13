using Application.Orders;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Infrastructure;

internal static class OrderHistoryEntriesConfiguration
{
    public static void Register()
    {
        var guidSerializer = new GuidSerializer(GuidRepresentation.Standard);

        BsonClassMap.TryRegisterClassMap<OrderHistoryEntry>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true); // without this, if you remove a field from the app then mongodb would crash when you try to read old records with the extra element  
            cm.SetIgnoreExtraElementsIsInherited(true);
            cm.MapIdMember(e => e.Id).SetSerializer(guidSerializer);
            cm.MapMember(e => e.OrderId).SetSerializer(guidSerializer);
        });

        BsonClassMap.TryRegisterClassMap<OrderConfirmedEntry>(cm =>
        {
            cm.AutoMap();
            cm.SetDiscriminator(OrderHistoryEntriesTypes.OrderConfirmedType);
            cm.SetDiscriminatorIsRequired(true);
            cm.MapMember(e => e.Total).SetSerializer(new DecimalSerializer(BsonType.Decimal128));
            cm.MapMember(e => e.VoucherId).SetSerializer(new NullableSerializer<Guid>(guidSerializer));
            cm.MapCreator(e => new OrderConfirmedEntry(e.Id, e.OrderId, e.OccurredAt, e.Total, e.VoucherId));
        });

        BsonClassMap.TryRegisterClassMap<OrderCanceledEntry>(cm =>
        {
            cm.AutoMap();
            cm.SetDiscriminator(OrderHistoryEntriesTypes.OrderCanceledType);
            cm.SetDiscriminatorIsRequired(true);
            cm.MapCreator(e => new OrderCanceledEntry(e.Id, e.OrderId, e.OccurredAt, e.Reason));
        });
    }

    public static async Task CreateIndexesAsync(IMongoDatabase database, CancellationToken ct)
    {
        // Without this index, a query filtered by OrderId should scan the entire collection 
        var ixOrderIdOccurredAt = new CreateIndexModel<OrderHistoryEntry>(
            Builders<OrderHistoryEntry>.IndexKeys.Ascending(e => e.OrderId).Ascending(e => e.OccurredAt),
            new CreateIndexOptions { Name = "orderId_occurredAt" }
        );

        await database.GetCollection<OrderHistoryEntry>(OrderHistoryLog.CollectionName)
                      .Indexes
                      .CreateOneAsync(ixOrderIdOccurredAt, cancellationToken: ct);
    }
}