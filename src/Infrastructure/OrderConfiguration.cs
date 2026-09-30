using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure;

internal class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever(); // Useful because I generate the Guid in the app and not on DB, Otherwise EF could try to update an non-existant item on the db, this happens with sub-aggregates like OrderLine
        builder.Property(o => o.Status).HasConversion<string>(); // enum as string is more readable in DB 
        builder.HasMany(o => o.Lines)
                .WithOne()
                .HasForeignKey("OrderId") // Shadow property that will exist only on DB
                .IsRequired() // This parameter is not mandatory, I add it here to make explicit that Line cannot exist without Order. 
                              // EF would work even without this because GUID is not nullable.
                .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Order.Lines))!
                        .SetPropertyAccessMode(PropertyAccessMode.Field); // EF will use the backing field _lines and not the property

        builder.Ignore(o => o.Total); 
    }
}

internal class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(o => o.Id).ValueGeneratedNever(); // Useful because I generate the Guid in the app and not on DB, Otherwise EF could try to update an non-existant item on the db, this happens with sub-aggregates like OrderLine
        builder.Property(l => l.UnitPriceSnapshot).HasPrecision(18, 2);
        builder.Property(l => l.ProductNameSnapshot).HasMaxLength(Product.NameMaxLength);
        builder.Property(l => l.Quantity);
        builder.Ignore(l => l.TotalPriceSnapshot); // Computed

        builder.ToTable(t => t.HasCheckConstraint("CK_OrderLines_UnitPriceSnapshot_NonNegative", "UnitPriceSnapshot >= 0"));
        builder.ToTable(t => t.HasCheckConstraint("CK_OrderLines_Quantity_Positive", "Quantity > 0"));
    }
}
