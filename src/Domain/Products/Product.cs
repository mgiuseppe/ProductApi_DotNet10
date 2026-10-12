using Domain.Exceptions;

namespace Domain.Products;

public class Product
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 1000;

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public decimal Price { get; private set; }
    public string? Description { get; private set; }

    private Product()
    {
        Name = "";
    }

    public static Product Create(string name, decimal price, string? description = null)
    {
        ValidateName(name);
        ValidatePrice(price);
        ValidateDescription(description);

        var p = new Product()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Price = price,
            Description = description
        };

        return p;
    }

    public Product Rename(string name)
    {
        ValidateName(name);
        Name = name;
        return this;
    }

    public Product ChangePrice(decimal price)
    {
        ValidatePrice(price);
        Price = price;
        return this;
    }

    public Product ChangeDescription(string? description)
    {
        ValidateDescription(description);
        
        Description = description;
        return this;
    }

    #region private validations
    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("name is required");
        if (name.Length > Product.NameMaxLength)
            throw new DomainException($"name is too long, max length is {NameMaxLength}");
    }
    private static void ValidatePrice(decimal price)
    {
        if (price < 0)
            throw new DomainException("price must be >= 0");
    }
    private static void ValidateDescription(string? description)
    {
        if (description != null && description.Length > Product.DescriptionMaxLength)
            throw new DomainException($"description is too long, max length is {DescriptionMaxLength}");
    }
    #endregion
}

