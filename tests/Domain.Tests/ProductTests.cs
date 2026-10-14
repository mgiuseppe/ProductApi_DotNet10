using System.Text;
using Domain.Exceptions;
using Domain.Products;

namespace Domain.Tests;

public class ProductTests
{
    [Fact]
    public void Create_WithValidValues_CreatesProduct()
    {
        //Arrange
        var name = "Test";
        var price = 0m;
        var description = "description";
        //Act
        var p = Product.Create(name, price, description);
        //Assert
        Assert.NotEqual(Guid.Empty, p.Id);
        Assert.Equal(name, p.Name);
        Assert.Equal(price, p.Price);
        Assert.Equal(description, p.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("    ")]
    public void Create_WithEmptyName_Throws(string name)
    {
        Assert.Throws<DomainException>(() => Product.Create(name, 10m, null));
    }

    [Fact]
    public void Create_WithNameTooLong_Throws()
    {
        Assert.Throws<DomainException>(() => Product.Create(new string('x', Product.NameMaxLength + 1), 10m, null));
    }

    [Fact]
    public void Create_WithNegativePrice_Throws()
    {
        Assert.Throws<DomainException>(() => Product.Create("Test", -0.01m, null));
    }

    [Fact]
    public void Create_WithDescriptionTooLong_Throws()
    {
        Assert.Throws<DomainException>(() => Product.Create("Test", 10m, new string('x', Product.DescriptionMaxLength + 1)));
    }

    [Fact]
    public void Rename_WithValidName_UpdatesName()
    {
        // Arrange
        var p = CreateValidProduct("Test");
        var newName = "Test2";
        // Act
        p.Rename(newName);
        // Assert
        Assert.Equal(newName, p.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public void Rename_WithEmptyName_Throws(string name)
    {
        // Arrange
        var p = CreateValidProduct("Test");
        // Act & Assert
        Assert.Throws<DomainException>(() => p.Rename(name));
    }

    [Fact]
    public void Rename_WithNameTooLong_Throws()
    {
        // Arrange
        var p = CreateValidProduct();
        // Act & Assert
        Assert.Throws<DomainException>(() => p.Rename(new string('x', Product.NameMaxLength + 1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.1)]
    public void ChangePrice_WithValidAmount_UpdatesPrice(double price)
    {
        // Arrange
        var dPrice = (decimal)price;
        var p = CreateValidProduct();
        // Act
        p.ChangePrice(dPrice);
        // Assert
        Assert.Equal(dPrice, p.Price);
    }

    [Fact]
    public void ChangePrice_WithNegativeAmount_Trows()
    {
        // Arrange
        var p = CreateValidProduct();
        // Act & Assert
        Assert.Throws<DomainException>(() => p.ChangePrice(-0.01m));
    }

    [Fact]
    public void ChangeDescription_WithTooLongDescription_Throws()
    {
        // Arrange
        var p = CreateValidProduct();
        // Act & Assert
        Assert.Throws<DomainException>(() => p.ChangeDescription(new string('x', Product.DescriptionMaxLength + 1)));
    }

    #region private helpers
    private static Product CreateValidProduct(string name = "Test", decimal price = 10m) => Product.Create(name, price, null);
    # endregion
}
