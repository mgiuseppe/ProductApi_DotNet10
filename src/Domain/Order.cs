using Domain.Exceptions;

namespace Domain;

public enum OrderStatus
{
    Draft,
    Confirmed,
    Shipped,
    Cancelled
}

/// <summary>
/// Order in status Draft acts as an implicit cart, see README § Order/Cart boundary for the reasoning, 
/// This is a deliberate simplification, a real e-commerce would use a separate cart aggregate.
/// 
/// Invariants:
/// - No rows with qty < 0
/// - No shipping without rows
/// - No edits on shipped or canceled rows
/// - Draft orders cannot be canceled because they should be deleted
/// </summary>
public class Order
{
    private readonly List<OrderLine> _lines = [];

    public Guid Id { get; private set; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();
    public decimal Total => _lines.Sum(l => l.TotalPriceSnapshot);

    private Order()
    {
    }

    public static Order Create()
    {
        var o = new Order
        {
            Id = Guid.NewGuid(),
            Status = OrderStatus.Draft
        };
        return o;
    }
    public Order AddLine(Guid productId, string productName, decimal unitPrice, int quantity)
    {
        ValidateCanEditLines();
        _lines.Add(OrderLine.Create(productId, productName, unitPrice, quantity));

        return this;
    }

    public Order UpdateLine(Guid id, decimal unitPrice, int quantity)
    {
        ValidateCanEditLines();
        var l = _lines.FirstOrDefault(l => l.Id == id) ?? throw new DomainException($"Order line '{id}' not found on order {Id}");

        l.ChangeUnitPrice(unitPrice);
        l.ChangeQuantity(quantity);

        return this;
    }
    public Order RemoveLine(Guid id)
    {
        ValidateCanEditLines();
        var l = _lines.FirstOrDefault(l => l.Id == id) ?? throw new DomainException($"Order line '{id}' not found on order {Id}");

        _lines.Remove(l);

        return this;
    }
    public void Confirm()
    {
        ValidateChangeStatus(OrderStatus.Confirmed);
        if (!_lines.Any())
            throw new DomainException("Cannot confirm order without lines");

        Status = OrderStatus.Confirmed;
    }
    public void Ship()
    {
        ValidateChangeStatus(OrderStatus.Shipped);
        if (!_lines.Any())
            throw new DomainException("Cannot ship order without lines");

        Status = OrderStatus.Shipped;
    }
    public void Cancel()
    {
        ValidateChangeStatus(OrderStatus.Cancelled);

        Status = OrderStatus.Cancelled;
    }

    #region private helpers
    private static readonly Dictionary<OrderStatus, HashSet<OrderStatus>> _allowedStatusTransitions = new()
    {
        { OrderStatus.Draft, [OrderStatus.Confirmed]},
        { OrderStatus.Confirmed, [OrderStatus.Shipped, OrderStatus.Cancelled]}
    };
    private void ValidateChangeStatus(OrderStatus newStatus)
    {
        if (!_allowedStatusTransitions.ContainsKey(Status) || !_allowedStatusTransitions[Status].Contains(newStatus))
            throw new DomainException($"Illegal OrderStatus change - old status: {Status} - new status: {newStatus}");
    }
    private void ValidateCanEditLines()
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException($"Can only edit lines for orders in status {OrderStatus.Draft}");
    }
    #endregion
}
