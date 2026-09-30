namespace Application.Commands;

public record CreateOrderCommand(IReadOnlyList<CreateOrderLine> Lines);
public record UpdateOrderCommand(IReadOnlyList<CreateOrderLine> LinesToAdd, 
                                 IReadOnlyList<UpdateOrderLine> LinesToUpdate, 
                                 IReadOnlyList<Guid> LineIdsToDelete);
public record CreateOrderLine(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity);
public record UpdateOrderLine(Guid Id, decimal UnitPrice, int Quantity);