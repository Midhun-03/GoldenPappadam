using GoldenPappadam.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements", Schemas.Inventory, table =>
            table.HasCheckConstraint("CK_StockMovements_QuantityNotZero", "[Quantity] <> 0"));

        builder.Property(x => x.MovementType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.ReferenceType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.Notes).HasMaxLength(300);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Location)
            .WithMany()
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Current stock per product at one location, and the movement history of one product.
        builder.HasIndex(x => new { x.ProductId, x.LocationId, x.OccurredAt });

        // "What is on the van right now?"
        builder.HasIndex(x => new { x.LocationId, x.OccurredAt });

        // "Which movements did this invoice or packing entry cause?"
        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
    }
}
