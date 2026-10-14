using Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Interceptors;

/// <summary>
/// This is a Learning example, not a production recipe.** Domain events are published after the commit and are
/// best-effort: a crash loses them, and an outer transaction that rolls back after the publication leaves
/// false records on MongoDB. For events that matter, write them in the same database, in the same transaction, or use an outbox.
/// </summary>
public class DispatchDomainEventsInterceptor(IMediator mediator, ILogger<DispatchDomainEventsInterceptor> logger) : SaveChangesInterceptor
{
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        DispatchDomainEvents(eventData).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await DispatchDomainEvents(eventData);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchDomainEvents(SaveChangesCompletedEventData data)
    {
        if (data.Context == null) return;

        var aggregates = data.Context.ChangeTracker
                                     .Entries<BaseAggregateRoot>()
                                     .Where(a => a.Entity.DomainEvents.Count > 0)
                                     .Select(a => a.Entity);
        var domainEvents = aggregates.SelectMany(a => a.DomainEvents)
                                     .ToList();
        
        aggregates.ToList().ForEach(a => a.ClearDomainEvents());

        foreach(var e in domainEvents)
        {
            try
            {
                await mediator.Publish(e);   
            }
            catch (Exception ex) // events are not critical, changes have already been saved
            {
                logger.LogWarning(ex, "event {EventType} handling failed", e.GetType());
            }
        }
        
        return;
    }
}
