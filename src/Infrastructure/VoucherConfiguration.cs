using Domain.Vouchers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure;

internal class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever(); // Useful because I generate the Guid in the app and not on DB, Otherwise EF could try to update an non-existant item on the db, this happens with sub-aggregates like OrderLine
        builder.Property(o => o.Amount).HasPrecision(18, 2);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Vouchers_Amount_Positive", "amount > 0");
        });
    }
}