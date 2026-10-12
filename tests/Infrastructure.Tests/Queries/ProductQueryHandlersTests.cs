using Application.Products;
using Domain.Products;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Queries;

// These tests live in Infrastructure.Tests (not Application.Tests) because 
// query handlers depend on the real AppDbContext (internal to Infrastructure) 
// to verify EF's actual SQL translation — see README § Testing strategy.
public class ProductQueryHandlersTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ProductQueryHandlersTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open(); // in-memory database exsists until the connection is open

        _options = new DbContextOptionsBuilder<AppDbContext>()
                                        .UseSqlite(_connection)
                                        .Options;

        using var db = new AppDbContext(_options);
        db.Database.Migrate(); // create model tables
    }

    private AppDbContext NewContext() => new(_options);
    public void Dispose() => _connection.Dispose();

    private async Task SeedAsync(params Product[] products)
    {
        await using var db = NewContext();
        db.Products.AddRange(products);
        await db.SaveChangesAsync();
    }

    // Integration Test
    [Fact]
    public async Task GetProductById_WithExistingId_ReturnsProduct()
    {
        var product = Product.Create("Keyboard", 49.90m, "Best in the world keyboard");
        await SeedAsync(product);

        await using var db = NewContext(); // new context to ensure db reading
        var found = await new GetProductByIdQueryHandler(db).Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(product.Id, found.Id);
        Assert.Equal("Keyboard", found.Name);
        Assert.Equal(49.90m, found.Price);
        Assert.Equal("Best in the world keyboard", found.Description);
    }

    // Integration Test
    [Fact]
    public async Task GetProductById_WithNonExistingId_ReturnsNull()
    {
        await using var db = NewContext();

        var found = await new GetProductByIdQueryHandler(db).Handle(new GetProductByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(found);
    }

    // Integration Test
    [Fact]
    public async Task GetProductsList_ReturnsProductsOrderedByName_WithPaging()
    {
        await SeedAsync(
            Product.Create("Banana", 1.50m),
            Product.Create("Chair", 89.99m),
            Product.Create("Apple", 0.99m));

        await using var db = NewContext();
        var handler = new GetProductsListQueryHandler(db);

        var firstPage = (await handler.Handle(new GetProductsListQuery(Page: 1, PageSize: 2), CancellationToken.None)).ToList();
        Assert.Equal(2, firstPage.Count);
        Assert.Equal("Apple", firstPage[0].Name);
        Assert.Equal("Banana", firstPage[1].Name);

        var secondPage = (await handler.Handle(new GetProductsListQuery(Page: 2, PageSize: 2), CancellationToken.None)).ToList();
        Assert.Single(secondPage);
        Assert.Equal("Chair", secondPage[0].Name);
    }
}
