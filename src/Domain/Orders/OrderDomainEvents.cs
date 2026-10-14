using Domain.Common;

namespace Domain.Orders;

public sealed record OrderConfirmedDomainEvent(Guid OrderId, decimal Total, Guid? VoucherId)
           : BaseDomainEvent;

public sealed record OrderCanceledDomainEvent(Guid OrderId)
           : BaseDomainEvent;