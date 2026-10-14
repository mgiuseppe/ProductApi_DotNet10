using Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests;

public class ProductRepositoryTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private DbContextOptions<AppDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync(); // own database, migrations already applied
        _options = _database.Options();
    }

    private AppDbContext NewContext() => new(_options);
    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

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
    public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
    {
        await using var db = NewContext();

        var found = await new ProductRepository(db).GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(found);
    }
}