using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure;

internal class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(nameof(Product.Id));
        builder.Property<string>(nameof(Product.Name)).HasMaxLength(Product.NameMaxLength).IsRequired();
        builder.Property<decimal>(nameof(Product.Price)).HasPrecision(18, 2);
        builder.Property<string>(nameof(Product.Description)).HasMaxLength(Product.DescriptionMaxLength);

        builder.ToTable(t => t.HasCheckConstraint("CK_Products_Price_NonNegative", "Price >= 0"));
    }
}
