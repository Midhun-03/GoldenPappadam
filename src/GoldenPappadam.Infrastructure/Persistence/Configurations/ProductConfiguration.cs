using GoldenPappadam.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", Schemas.Inventory, table =>
        {
            // A packed product must say what it is packed from; a loose product must not.
            table.HasCheckConstraint(
                "CK_Products_Source",
                "([Kind] = 'Packed' AND [SourceProductId] IS NOT NULL AND [SourceQuantityPerPack] > 0) " +
                "OR ([Kind] = 'Loose' AND [SourceProductId] IS NULL AND [SourceQuantityPerPack] IS NULL)");

            // The shortest possible cycle. Longer chains are checked in application code.
            table.HasCheckConstraint(
                "CK_Products_SourceNotSelf",
                "[SourceProductId] IS NULL OR [SourceProductId] <> [Id]");
        });

        builder.Property(x => x.ProductCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.SourceQuantityPerPack).HasPrecision(18, 3);
        builder.Property(x => x.SellingPrice).HasPrecision(18, 2);
        builder.Property(x => x.LowStockThreshold).HasPrecision(18, 3);

        builder.HasIndex(x => x.ProductCode).IsUnique();

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UnitOfMeasure)
            .WithMany()
            .HasForeignKey(x => x.UnitOfMeasureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SourceProduct)
            .WithMany()
            .HasForeignKey(x => x.SourceProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
