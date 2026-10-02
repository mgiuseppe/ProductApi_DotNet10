using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Products;

public record GetProductByIdQuery(Guid Id) : IRequest<ProductResponse?>;

public class GetProductByIdQueryHandler(IQueryContext db) : IRequestHandler<GetProductByIdQuery, ProductResponse?>
{
    public async Task<ProductResponse?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
            
        return product is null ? null : ProductResponse.FromDomain(product);
    }
}

