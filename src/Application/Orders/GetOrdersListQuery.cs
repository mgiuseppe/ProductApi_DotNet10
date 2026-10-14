using System;
using Application.Common.Interfaces;
using Application.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Orders;

public record GetOrdersListQuery(int Page, int PageSize) : IRequest<IReadOnlyList<OrderResponse>>;

public class GetOrdersListQueryHandler(IQueryContext db) : IRequestHandler<GetOrdersListQuery, IReadOnlyList<OrderResponse>>
{
    public async Task<IReadOnlyList<OrderResponse>> Handle(GetOrdersListQuery request, CancellationToken cancellationToken)
    {
        return await db.Orders
                        .AsNoTracking()
                        .OrderBy(o => o.Id)
                        .Skip((request.Page - 1) * request.PageSize)
                        .Take(request.PageSize)
                        .Select(o => OrderResponse.FromDomain(o)) // FromDomain runs in memory after the query is executed without projection, 
                                                                  // if you have many fields, don't use a c# function in select but map the fields
                                                                  // directly to a DTO so taht EF can do the project that database side
                        .ToListAsync(cancellationToken);
    }
}
