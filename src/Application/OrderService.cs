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
        var o = Order.Create();
        foreach(var l in cmd.Lines)
            o.AddLine(l.ProductId, l.ProductName, l.UnitPrice, l.Quantity);
        
        // TODO: check products and bring their data from server

        await _ordersRepository.AddAsync(o, ct);
        await _uow.SaveChangesAsync(ct);
        
        return o;
    }
    
    public async Task<Order> UpdateAsync(Guid id, UpdateOrderCommand cmd, CancellationToken ct)
    {
        var o = await _ordersRepository.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Order {id} not found");
        
        // TODO: check products and bring their data from server

        foreach(var l in cmd.LinesToAdd)
        {
            o.AddLine(l.ProductId, l.ProductName, l.UnitPrice, l.Quantity);
        }
        foreach(var l in cmd.LinesToUpdate)
        {
            o.UpdateLine(l.Id, l.UnitPrice, l.Quantity);
        }
        foreach(var lineId in cmd.LineIdsToDelete)
        {
            o.RemoveLine(lineId);
        }

        await _uow.SaveChangesAsync(ct);
        
        return o;
    }

    public async Task<Order> ConfirmAsync (Guid id, CancellationToken ct)
    {
        return await RetrieveAndChangeStatusAsync(id, o => o.Confirm(), ct);
    }
    public async Task<Order> ShipAsync (Guid id, CancellationToken ct)
    {
        return await RetrieveAndChangeStatusAsync(id, o => o.Ship(), ct);
    }
    public async Task<Order> CancelAsync (Guid id, CancellationToken ct)
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
    #endregion
}
