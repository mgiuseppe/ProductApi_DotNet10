using Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests;

public class ProductRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ProductRepositoryTests()
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

    // Integration Test
    [Fact]
    public async Task Add_ThenGetById_ReturnsSavedProduct()
    {
        var name = "Keyboard";
        var description = "Best in the world keyboard";
        var price = 49.90m;
        var product = Product.Create(name, price, description);

        await using (var db = NewContext())
        {
            await new ProductRepository(db).AddAsync(product, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext()) // new context to ensure db reading
        {
            var found = await new ProductRepository(db).GetByIdAsync(product.Id, CancellationToken.None);

            Assert.NotNull(found);
            Assert.Equal(name, found.Name);
            Assert.Equal(price, found.Price);
            Assert.Equal(description, found.Description);
        }
    }

    // Integration Test
    [Fact]
    public async Task Update_Rename_PersistsNewName()
    {
        var product = Product.Create("Keyboard", 49.90m, "Best in the world keyboard");
        var newName = "Mechanical Keyboard";

        await using (var db = NewContext())
        {
            await new ProductRepository(db).AddAsync(product, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext()) // new context to load, rename and save
        {
            var repository = new ProductRepository(db);
            var loaded = await repository.GetByIdAsync(product.Id, CancellationToken.None);

            Assert.NotNull(loaded);
            loaded.Rename(newName);

            await db.SaveChangesAsync();
        }

        await using (var db = NewContext()) // yet another context to verify the new name
        {
            var found = await new ProductRepository(db).GetByIdAsync(product.Id, CancellationToken.None);

            Assert.NotNull(found);
            Assert.Equal(newName, found.Name);
        }
    }

    // Integration Test
    [Fact]
    public async Task Delete_ThenGetById_ReturnsNull()
    {
        var product = Product.Create("Keyboard", 49.90m, "Best in the world keyboard");

        await using (var db = NewContext())
        {
            var repository = new ProductRepository(db);
            await repository.AddAsync(product, CancellationToken.None);
            await db.SaveChangesAsync();

            var loaded = await repository.GetByIdAsync(product.Id, CancellationToken.None);
            Assert.NotNull(loaded);

            repository.Delete(loaded);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext()) // new context to ensure db reading
        {
            var found = await new ProductRepository(db).GetByIdAsync(product.Id, CancellationToken.None);

            Assert.Null(found);
        }
    }

    // Integration Test
    [Fact]
    public async Task ListProductsAsync_ReturnsProductsOrderedByName_WithPaging()
    {
        var productB = Product.Create("Banana", 1.50m);
        var productC = Product.Create("Chair", 89.99m);
        var productA = Product.Create("Apple", 0.99m);

        await using (var db = NewContext())
        {
            var repository = new ProductRepository(db);
            await repository.AddAsync(productB, CancellationToken.None);
            await repository.AddAsync(productC, CancellationToken.None);
            await repository.AddAsync(productA, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var repository = new ProductRepository(db);

            var firstPage = await repository.ListProductsAsync(page: 1, pageSize: 2, CancellationToken.None);
            Assert.Equal(2, firstPage.Count);
            Assert.Equal("Apple", firstPage[0].Name);
            Assert.Equal("Banana", firstPage[1].Name);

            var secondPage = await repository.ListProductsAsync(page: 2, pageSize: 2, CancellationToken.None);
            Assert.Single(secondPage);
            Assert.Equal("Chair", secondPage[0].Name);
        }
    }

    // Integration Test
    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
    {
        await using var db = NewContext();

        var found = await new ProductRepository(db).GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(found);
    }
}