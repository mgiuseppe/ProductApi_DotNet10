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
            .Select(p => ProductResponse.FromDomain(p))
            .ToListAsync(cancellationToken);
    }
}
