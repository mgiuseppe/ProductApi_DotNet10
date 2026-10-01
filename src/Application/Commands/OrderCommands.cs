namespace Application.Commands;

public record CreateOrderCommand(IReadOnlyList<CreateOrderLine> Lines);
public record UpdateOrderCommand(IReadOnlyList<CreateOrderLine> LinesToAdd, 
                                 IReadOnlyList<UpdateOrderLine> LinesToUpdate, 
                                 IReadOnlyList<Guid> LineIdsToDelete);
public record CreateOrderLine(Guid ProductId, int Quantity);
public record UpdateOrderLine(Guid Id, int Quantity);