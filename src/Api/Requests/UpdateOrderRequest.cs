using Application.Orders;

namespace Api.Requests;

public record UpdateOrderRequest(IReadOnlyList<CreateOrderLine> LinesToAdd,
                                 IReadOnlyList<UpdateOrderLine> LinesToUpdate,
                                 IReadOnlyList<Guid> LineIdsToDelete);
