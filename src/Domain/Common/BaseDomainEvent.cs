using MediatR;

namespace Domain.Common;

/// <summary>
/// Domain references MediatR.Contracts only (INotification) 
/// to avoid a wrapper and reflection in the dispatcher.
/// 
/// This is a deliberate, small exception to a dependency-free Domain.
/// </summary>
public abstract record BaseDomainEvent : INotification
{
    public Guid EventId { get; } = Guid.CreateVersion7();
}
