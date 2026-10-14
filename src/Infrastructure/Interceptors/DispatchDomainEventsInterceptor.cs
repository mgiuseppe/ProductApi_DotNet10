using Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Interceptors;

/// <summary>
/// This is a Learning example, not a complete production recipe.
/// Domain events are published and handled before the commit. 
/// If a handler throws, changes are not committed and you should add trycatch if it's not intended to work in this way. 
/// If the transaction is rolled back, records on mongodb are still persisted and become orphans because they're not under the same transaction as EF.
/// See DomainEventsHandling.md for further info.
/// </summary>
public class DispatchDomainEventsInterceptor(IMediator mediator) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        DispatchDomainEvents(eventData, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavingChanges(eventData, result);
    }

    public async override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await DispatchDomainEvents(eventData, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchDomainEvents(DbContextEventData data, CancellationToken cancellationToken)
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
            await mediator.Publish(e, cancellationToken);
        
        return;
    }
}
