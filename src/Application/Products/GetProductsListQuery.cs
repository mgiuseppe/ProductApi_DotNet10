using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Products;

public record GetProductsListQuery(int Page, int PageSize) : IRequest<IReadOnlyList<ProductResponse>>;

public class GetProductsListQueryHandler(IQueryContext db) : IRequestHandler<GetProductsListQuery, IReadOnlyList<ProductResponse>>
{
    public async Task<IReadOnlyList<ProductResponse>> Handle(GetProductsListQuery request, CancellationToken cancellationToken)
    {
        return await db.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => ProductResponse.FromDomain(p)) // FromDomain runs in memory after the query is executed without projection, 
                                                        // if you have many fields, don't use a c# function in select but map the fields
                                                        // directly to a DTO so taht EF can do the project that database side
            .ToListAsync(cancellationToken);
    }
}
