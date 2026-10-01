using Application.Commands;
using Application.Exceptions;
using Domain;

namespace Application;

public class OrderService(IOrderRepository repository, IProductRepository productRepository, IUnitOfWork uow) : IOrderService
{
    private readonly IOrderRepository _ordersRepository = repository;
    private readonly IProductRepository _productsRepository = productRepository;
    private readonly IUnitOfWork _uow = uow;

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _ordersRepository.GetByIdAsync(id, ct);
    }

    public async Task<IReadOnlyList<Order>> GetListAsync(int page, int pageSize, CancellationToken ct)
    {
        return await _ordersRepository.ListOrdersAsync(page, pageSize, ct);
    }

    public async Task<Order> CreateAsync(CreateOrderCommand cmd, CancellationToken ct)
    {
        var prodsById = await GetProductsOrThrowAsync(cmd.Lines.Select(l => l.ProductId).Distinct(), ct);

        var o = Order.Create();
        foreach (var l in cmd.Lines)
        {
            var prod = prodsById[l.ProductId];
            o.AddLine(l.ProductId, prod.Name, prod.Price, l.Quantity);
        }

        await _ordersRepository.AddAsync(o, ct);
        await _uow.SaveChangesAsync(ct);

        return o;
    }

    public async Task<Order> UpdateAsync(Guid id, UpdateOrderCommand cmd, CancellationToken ct)
    {
        var o = await _ordersRepository.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Order {id} not found");
        var prodsById = await GetProductsOrThrowAsync(cmd.LinesToAdd.Select(l => l.ProductId)
                                                              .Union(o.Lines.Select(l => l.ProductId))
                                                              .Distinct(), ct);

        foreach (var l in cmd.LinesToAdd)
        {
            var prod = prodsById[l.ProductId];
            o.AddLine(l.ProductId, prod.Name, prod.Price, l.Quantity);
        }
        foreach (var l in cmd.LinesToUpdate)
        {
            var existingLine = o.Lines.FirstOrDefault(line => line.Id == l.Id) ?? throw new NotFoundException($"Line {l.Id} not found");
            var prod = prodsById[existingLine.ProductId];
            o.UpdateLine(l.Id, prod.Price, l.Quantity);
        }
        foreach (var lineId in cmd.LineIdsToDelete)
        {
            o.RemoveLine(lineId);
        }

        await _uow.SaveChangesAsync(ct);

        return o;
    }

    /// <summary>
    /// Silently re-syncs stale line prices before confirming (Order.Draft acts as a live-priced cart).
    /// See README § Order/Cart boundary for the reasoning — this is a deliberate simplification: 
    /// A real e-commerce would surface price changes to the user before checkout.
    /// </summary>
    public async Task<Order> ConfirmAsync(Guid id, CancellationToken ct)
    {
        var o = await _ordersRepository.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Order {id} not found");
        var prodsById = await GetProductsOrThrowAsync(o.Lines.Select(l => l.ProductId), ct);

        foreach (var l in o.Lines)
        {
            var prod = prodsById[l.ProductId];
            if (prod.Price != l.UnitPriceSnapshot)
                o.UpdateLine(l.Id, prod.Price, l.Quantity);
        }

        o.Confirm();
        await _uow.SaveChangesAsync(ct);

        return o;
    }
    public async Task<Order> ShipAsync(Guid id, CancellationToken ct)
    {
        return await RetrieveAndChangeStatusAsync(id, o => o.Ship(), ct);
    }
    public async Task<Order> CancelAsync(Guid id, CancellationToken ct)
    {
        return await RetrieveAndChangeStatusAsync(id, o => o.Cancel(), ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var o = await _ordersRepository.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Order {id} not found");

        _ordersRepository.Delete(o);
        await _uow.SaveChangesAsync(ct);
    }

    #region private helpers
    private async Task<Order> RetrieveAndChangeStatusAsync(Guid id, Action<Order> apply, CancellationToken ct)
    {
        var o = await _ordersRepository.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Order {id} not found");

        apply(o);
        await _uow.SaveChangesAsync(ct);

        return o;
    }

    private async Task<Dictionary<Guid, Product>> GetProductsOrThrowAsync(IEnumerable<Guid> productIds, CancellationToken ct)
    {
        var ids = productIds.Distinct().ToList();
        var prods = await _productsRepository.GetByIdsAsync(ids, ct);
        var prodsById = prods.ToDictionary(p => p.Id);

        var missing = ids.Except(prodsById.Keys).ToList();
        if (missing.Count > 0)
            throw new NotFoundException($"Product(s) not found: {string.Join(", ", missing)}");

        return prodsById;
    }
    #endregion
}
