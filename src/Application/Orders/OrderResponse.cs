using Domain.Orders;

namespace Application.Responses;

// TODO: add total
public record OrderResponse(Guid Id, OrderStatus Status)
{
    public static OrderResponse FromDomain(Order o)
    {
        return new OrderResponse(o.Id, o.Status);
    }
}

/// <param name="Total">Sum of the line totals minus the voucher discount, if any.</param>
public record OrderWithLinesResponse(Guid Id, OrderStatus Status, decimal Total, IReadOnlyList<OrderLineResponse> Lines)
{
    public static OrderWithLinesResponse FromDomain(Order o)
    {
        return new OrderWithLinesResponse(o.Id,
                                          o.Status,
                                          o.Total,
                                          o.Lines.Select(l => OrderLineResponse.FromDomain(l)).ToList());
    }
}

/// <param name="Id">Id of the order line, not of the product.</param>
/// <param name="ProductNameSnapshot">Product name when the line was added: later renames of the product do not change it.</param>
/// <param name="UnitPriceSnapshot">Product price copied into the line when it is added or updated, and again at confirmation; fixed after that.</param>
/// <param name="TotalPriceSnapshot">Unit price times quantity.</param>
public record OrderLineResponse(Guid Id, Guid ProductId, string ProductNameSnapshot, decimal UnitPriceSnapshot, int Quantity, decimal TotalPriceSnapshot)
{
    public static OrderLineResponse FromDomain(OrderLine l)
    {
        return new OrderLineResponse(l.Id,
                                     l.ProductId,
                                     l.ProductNameSnapshot,
                                     l.UnitPriceSnapshot,
                                     l.Quantity,
                                     l.TotalPriceSnapshot);
    }
}