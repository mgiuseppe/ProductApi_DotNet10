using Domain.Exceptions;

namespace Domain.Tests;

public class OrderTests
{
    [Fact]
    public void Create_CreatesDraftOrderWithIdAndNoLines()
    {
        // Act
        var o = Order.Create();
        // Assert
        Assert.NotEqual(Guid.Empty, o.Id);
        Assert.Equal(OrderStatus.Draft, o.Status);
        Assert.Empty(o.Lines);
        Assert.Equal(0m, o.Total);
    }

    [Fact]
    public void AddLine_WithValidValues_CreatesLineWithId()
    {
        // Arrange
        var o = Order.Create();
        var productId = Guid.NewGuid();
        // Act
        o.AddLine(productId, "Test", 10m, 2);
        // Assert
        var l = Assert.Single(o.Lines);
        Assert.NotEqual(Guid.Empty, l.Id);
        Assert.Equal(productId, l.ProductId);
        Assert.Equal("Test", l.ProductNameSnapshot);
        Assert.Equal(10m, l.UnitPriceSnapshot);
        Assert.Equal(2, l.Quantity);
    }

    [Fact]
    public void AddLine_MultipleLines_CreatesDistinctIds()
    {
        // Arrange
        var o = Order.Create();
        // Act
        o.AddLine(Guid.NewGuid(), "A", 1m, 1)
         .AddLine(Guid.NewGuid(), "B", 1m, 1);
        // Assert
        Assert.Equal(2, o.Lines.Select(l => l.Id).Distinct().Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddLine_WithInvalidQuantity_Throws(int quantity)
    {
        var o = Order.Create();
        Assert.Throws<DomainException>(() => o.AddLine(Guid.NewGuid(), "Test", 10m, quantity));
    }

    [Fact]
    public void AddLine_WithNegativePrice_Throws()
    {
        var o = Order.Create();
        Assert.Throws<DomainException>(() => o.AddLine(Guid.NewGuid(), "Test", -0.01m, 1));
    }

    [Fact]
    public void AddLine_WithEmptyProductId_Throws()
    {
        var o = Order.Create();
        Assert.Throws<DomainException>(() => o.AddLine(Guid.Empty, "Test", 10m, 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void AddLine_WithEmptyProductName_Throws(string? name)
    {
        var o = Order.Create();
        Assert.Throws<DomainException>(() => o.AddLine(Guid.NewGuid(), name!, 10m, 1));
    }

    [Fact]
    public void AddLine_WithProductNameTooLong_Throws()
    {
        var o = Order.Create();
        Assert.Throws<DomainException>(() => o.AddLine(Guid.NewGuid(), new string('x', Product.NameMaxLength + 1), 10m, 1));
    }

    [Fact]
    public void UpdateLine_WithValidValues_UpdatesLine()
    {
        // Arrange
        var o = CreateOrderWithLine();
        var lineId = o.Lines.Single().Id;
        // Act
        o.UpdateLine(lineId, 5m, 3);
        // Assert
        var l = o.Lines.Single();
        Assert.Equal(5m, l.UnitPriceSnapshot);
        Assert.Equal(3, l.Quantity);
    }

    [Fact]
    public void UpdateLine_WithUnknownId_Throws()
    {
        var o = CreateOrderWithLine();
        Assert.Throws<DomainException>(() => o.UpdateLine(Guid.NewGuid(), 5m, 3));
    }

    [Fact]
    public void RemoveLine_WithExistingId_RemovesLine()
    {
        // Arrange
        var o = CreateOrderWithLine();
        var lineId = o.Lines.Single().Id;
        // Act
        o.RemoveLine(lineId);
        // Assert
        Assert.Empty(o.Lines);
    }

    [Fact]
    public void RemoveLine_WithUnknownId_Throws()
    {
        var o = CreateOrderWithLine();
        Assert.Throws<DomainException>(() => o.RemoveLine(Guid.NewGuid()));
    }

    [Fact]
    public void Total_MatchesSumOfUnitPriceTimesQuantity()
    {
        // Arrange
        var o = Order.Create();
        // Act
        o.AddLine(Guid.NewGuid(), "A", 10m, 2)
         .AddLine(Guid.NewGuid(), "B", 2.5m, 4)
         .AddLine(Guid.NewGuid(), "C", 0.99m, 3);
        // Assert
        Assert.Equal(10m * 2 + 2.5m * 4 + 0.99m * 3, o.Total);
        Assert.Equal(o.Lines.Sum(l => l.UnitPriceSnapshot * l.Quantity), o.Total);
    }

    [Fact]
    public void Total_IsRecalculatedAfterUpdateAndRemove()
    {
        // Arrange
        var o = Order.Create();
        o.AddLine(Guid.NewGuid(), "A", 10m, 2)
         .AddLine(Guid.NewGuid(), "B", 2.5m, 4);
        var lines = o.Lines.ToList();
        // Act & Assert
        o.UpdateLine(lines[0].Id, 3m, 5);
        Assert.Equal(3m * 5 + 2.5m * 4, o.Total);

        o.RemoveLine(lines[1].Id);
        Assert.Equal(3m * 5, o.Total);
    }

    [Fact]
    public void Confirm_FromDraftWithLines_SetsConfirmed()
    {
        var o = CreateOrderWithLine();
        o.Confirm();
        Assert.Equal(OrderStatus.Confirmed, o.Status);
    }

    [Fact]
    public void Confirm_WithoutLines_Throws()
    {
        var o = Order.Create();
        Assert.Throws<DomainException>(() => o.Confirm());
        Assert.Equal(OrderStatus.Draft, o.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled)]
    public void ChangeStatus_FromConfirmed_IsAllowed(OrderStatus target)
    {
        // Arrange
        var o = CreateOrderInStatus(OrderStatus.Confirmed);
        // Act
        ChangeStatus(o, target);
        // Assert
        Assert.Equal(target, o.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Draft, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Draft, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Cancelled)]
    public void ChangeStatus_IllegalTransition_Throws(OrderStatus from, OrderStatus to)
    {
        // Arrange
        var o = CreateOrderInStatus(from);
        // Act & Assert
        Assert.Throws<DomainException>(() => ChangeStatus(o, to));
        Assert.Equal(from, o.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled)]
    public void AddLine_WhenNotDraft_Throws(OrderStatus status)
    {
        var o = CreateOrderInStatus(status);
        Assert.Throws<DomainException>(() => o.AddLine(Guid.NewGuid(), "Other", 1m, 1));
        Assert.Single(o.Lines);
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled)]
    public void UpdateLine_WhenNotDraft_Throws(OrderStatus status)
    {
        // Arrange
        var o = CreateOrderInStatus(status);
        var l = o.Lines.Single();
        var (price, quantity) = (l.UnitPriceSnapshot, l.Quantity);
        // Act & Assert
        Assert.Throws<DomainException>(() => o.UpdateLine(l.Id, price + 1, quantity + 1));
        Assert.Equal(price, l.UnitPriceSnapshot);
        Assert.Equal(quantity, l.Quantity);
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled)]
    public void RemoveLine_WhenNotDraft_Throws(OrderStatus status)
    {
        var o = CreateOrderInStatus(status);
        Assert.Throws<DomainException>(() => o.RemoveLine(o.Lines.Single().Id));
        Assert.Single(o.Lines);
    }

    #region private helpers
    private static Order CreateOrderWithLine() => Order.Create().AddLine(Guid.NewGuid(), "Test", 10m, 2);

    private static Order CreateOrderInStatus(OrderStatus status)
    {
        var o = CreateOrderWithLine();
        if (status == OrderStatus.Draft)
            return o;

        o.Confirm();
        if (status != OrderStatus.Confirmed)
            ChangeStatus(o, status);

        return o;
    }

    private static void ChangeStatus(Order o, OrderStatus target)
    {
        switch (target)
        {
            case OrderStatus.Confirmed: o.Confirm(); break;
            case OrderStatus.Shipped: o.Ship(); break;
            case OrderStatus.Cancelled: o.Cancel(); break;
            default: throw new ArgumentOutOfRangeException(nameof(target), target, "No transition method for this status");
        }
    }
    #endregion
}
