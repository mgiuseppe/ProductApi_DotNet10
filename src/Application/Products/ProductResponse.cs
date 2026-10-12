using Domain.Products;

namespace Application.Products;

public record ProductResponse(Guid Id, string Name, decimal Price, string? Description)
{
    public static ProductResponse FromDomain(Product p)
    {
        return new ProductResponse(p.Id, p.Name, p.Price, p.Description);
    }
}