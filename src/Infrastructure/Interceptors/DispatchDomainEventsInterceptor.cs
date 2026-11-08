using Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Interceptors;

/// <summary>
/// Not a complete production recipe.
/// Domain events are published and handled before the commit.
/// If a handler throws, nothing is committed: add a try/catch in the handler if that is not the intended behavior.
/// If the transaction is rolled back, the records already written to MongoDB stay and become orphans, because MongoDB is not in the EF transaction.
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

        foreach (var e in domainEvents)
            await mediator.Publish(e, cancellationToken);

        return;
    }
}
