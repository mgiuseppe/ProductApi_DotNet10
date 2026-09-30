using Domain;

namespace Application.Responses;

// TODO: add total
public record OrderResponse(Guid Id, OrderStatus Status)
{
    public static OrderResponse FromDomain(Order o)
    {
        return new OrderResponse(o.Id, o.Status);
    }
}

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