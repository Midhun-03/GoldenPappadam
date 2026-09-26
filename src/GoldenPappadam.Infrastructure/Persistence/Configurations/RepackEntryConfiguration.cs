using GoldenPappadam.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class RepackEntryConfiguration : IEntityTypeConfiguration<RepackEntry>
{
    public void Configure(EntityTypeBuilder<RepackEntry> builder)
    {
        builder.ToTable("RepackEntries", Schemas.Inventory, table =>
        {
            table.HasCheckConstraint("CK_RepackEntries_FromQuantity", "[FromQuantity] > 0");
            table.HasCheckConstraint("CK_RepackEntries_ToQuantity", "[ToQuantity] > 0");
            table.HasCheckConstraint(
                "CK_RepackEntries_Leftover",
                "([LeftoverProductId] IS NULL AND [LeftoverQuantity] = 0) OR ([LeftoverProductId] IS NOT NULL AND [LeftoverQuantity] > 0)");
        });

        builder.Property(x => x.FromQuantity).HasPrecision(18, 3);
        builder.Property(x => x.ToQuantity).HasPrecision(18, 3);
        builder.Property(x => x.LeftoverQuantity).HasPrecision(18, 3);
        builder.Property(x => x.Notes).HasMaxLength(300);

        builder.HasOne(x => x.FromProduct).WithMany().HasForeignKey(x => x.FromProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ToProduct).WithMany().HasForeignKey(x => x.ToProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LeftoverProduct).WithMany().HasForeignKey(x => x.LeftoverProductId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.OccurredAt);
    }
}
