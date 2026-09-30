using GoldenPappadam.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class PackingEntryConfiguration : IEntityTypeConfiguration<PackingEntry>
{
    public void Configure(EntityTypeBuilder<PackingEntry> builder)
    {
        builder.ToTable("PackingEntries", Schemas.Inventory, table =>
        {
            table.HasCheckConstraint("CK_PackingEntries_PacksProduced", "[PacksProduced] > 0");
            table.HasCheckConstraint("CK_PackingEntries_SourceQuantityUsed", "[SourceQuantityUsed] > 0");
            table.HasCheckConstraint("CK_PackingEntries_DifferentProducts", "[PackedProductId] <> [SourceProductId]");
        });

        builder.Property(x => x.PacksProduced).HasPrecision(18, 3);
        builder.Property(x => x.SourceQuantityUsed).HasPrecision(18, 3);
        builder.Property(x => x.Notes).HasMaxLength(300);
        builder.Property(x => x.PiecesPerKg).HasPrecision(18, 3);

        // Enough places that 20 pieces at 170 per kg (0.117647... kg) is kept as it was used.
        builder.Property(x => x.SourcePerPack).HasPrecision(18, 6);
        builder.Property(x => x.SourceOnHandBefore).HasPrecision(18, 3);

        // The same packing sent twice is packed once.
        builder.HasIndex(x => x.ClientRequestId)
            .IsUnique()
            .HasFilter("[ClientRequestId] IS NOT NULL");

        builder.HasOne(x => x.PackedProduct)
            .WithMany()
            .HasForeignKey(x => x.PackedProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SourceProduct)
            .WithMany()
            .HasForeignKey(x => x.SourceProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.OccurredAt);
    }
}
