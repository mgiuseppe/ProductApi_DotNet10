using Domain.Exceptions;
using Domain.Products;

namespace Domain.Orders;

public class OrderLine
{
    public Guid Id {get; private set;}
    public Guid ProductId {get; private set;}
    public string ProductNameSnapshot {get; private set;} = "";
    public decimal UnitPriceSnapshot {get; private set;} 
    public int Quantity {get; private set;}

    public decimal TotalPriceSnapshot => UnitPriceSnapshot * Quantity; 

    private OrderLine()
    {
    }

    internal static OrderLine Create(Guid productId, string productName, decimal unitPrice, int quantity)
    {
        ValidatePrice(unitPrice);
        ValidateQuantity(quantity);

        if (productId == Guid.Empty)
            throw new DomainException("Cannot add order line with empty product id");
        if (string.IsNullOrEmpty(productName))
            throw new DomainException("Cannot add order line with empty product name");
        if (productName.Length > Product.NameMaxLength)
            throw new DomainException($"Cannot add order line with product name longer than {Product.NameMaxLength}");

        return new OrderLine
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductNameSnapshot = productName,
            UnitPriceSnapshot = unitPrice,
            Quantity = quantity
        };
    }

    internal void ChangeUnitPrice(decimal price)
    {
        ValidatePrice(price);
        UnitPriceSnapshot = price;
    }

    internal void ChangeQuantity(int quantity)
    {
        ValidateQuantity(quantity);
        Quantity = quantity;
    }

    #region private helpers
    private static void ValidatePrice(decimal price)
    {
        if (price < 0)
            throw new DomainException($"Cannot add order line with price lower than 0");
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException($"Cannot add order line with quantity lower then 1");
    }
    #endregion
}