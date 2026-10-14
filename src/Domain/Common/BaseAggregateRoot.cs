namespace Domain.Common;

/// <summary>
/// Base class for aggregate roots: collects the domain events raised during a use case.
/// In DDD domain events are raised only by the aggregate root because it's the only entry 
/// point of the aggregate.
/// </summary>
public abstract class BaseAggregateRoot
{
    private readonly List<BaseDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<BaseDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(BaseDomainEvent e) => _domainEvents.Add(e);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
