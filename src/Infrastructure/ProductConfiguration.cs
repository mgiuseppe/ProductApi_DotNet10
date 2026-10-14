using Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure;

internal class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(o => o.Id).ValueGeneratedNever(); // Useful because I generate the Guid in the app and not on DB, Otherwise EF could try to update an non-existant item on the db, this happens with sub-aggregates like OrderLine
        builder.Property(p => p.Name).HasMaxLength(Product.NameMaxLength).IsRequired();
        builder.Property(p => p.Price).HasPrecision(18, 2);
        builder.Property(p => p.Description).HasMaxLength(Product.DescriptionMaxLength);

        builder.ToTable(t => t.HasCheckConstraint("CK_Products_Price_NonNegative", "price >= 0"));
    }
}
