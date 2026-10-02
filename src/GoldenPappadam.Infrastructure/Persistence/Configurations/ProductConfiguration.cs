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
            // A packed product says what it is packed from and how much one pack holds - a quantity of
            // the source, or a number of pieces - never both. A loose product says neither. An own-shop
            // pieces product names its loose variety and nothing else: one piece is one piece.
            table.HasCheckConstraint(
                "CK_Products_Source",
                "([Kind] = 'Packed' AND [SourceProductId] IS NOT NULL AND " +
                "(([SourceQuantityPerPack] > 0 AND [PiecesPerPack] IS NULL) OR " +
                "([SourceQuantityPerPack] IS NULL AND [PiecesPerPack] > 0))) " +
                "OR ([Kind] = 'Loose' AND [SourceProductId] IS NULL AND [SourceQuantityPerPack] IS NULL " +
                "AND [PiecesPerPack] IS NULL) " +
                "OR ([Kind] = 'Pieces' AND [SourceProductId] IS NOT NULL AND [SourceQuantityPerPack] IS NULL " +
                "AND [PiecesPerPack] IS NULL)");

            // The own shop's rate band: minimum up to the standard rate, on pieces products only.
            table.HasCheckConstraint(
                "CK_Products_MinimumSellingPrice",
                "[MinimumSellingPrice] IS NULL OR ([Kind] = 'Pieces' AND [MinimumSellingPrice] > 0 " +
                "AND [SellingPrice] IS NOT NULL AND [MinimumSellingPrice] <= [SellingPrice])");

            // Pieces per kg describes a loose variety.
            table.HasCheckConstraint(
                "CK_Products_PiecesPerKg",
                "[PiecesPerKg] IS NULL OR ([Kind] = 'Loose' AND [PiecesPerKg] > 0)");

            // The shortest possible cycle. Longer chains are checked in application code.
            table.HasCheckConstraint(
                "CK_Products_SourceNotSelf",
                "[SourceProductId] IS NULL OR [SourceProductId] <> [Id]");

            table.HasCheckConstraint("CK_Products_ShelfLifeDays", "[ShelfLifeDays] IS NULL OR [ShelfLifeDays] > 0");

            // A rate belongs to taxable products only; exempt and nil-rated goods carry none.
            table.HasCheckConstraint(
                "CK_Products_GstRate",
                "([TaxTreatment] = 'Taxable' AND [GstRate] > 0 AND [GstRate] <= 100) " +
                "OR (([TaxTreatment] IS NULL OR [TaxTreatment] <> 'Taxable') AND [GstRate] IS NULL)");
        });

        builder.Property(x => x.ProductCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.SourceQuantityPerPack).HasPrecision(18, 3);
        builder.Property(x => x.PiecesPerKg).HasPrecision(18, 3);
        builder.Property(x => x.SellingPrice).HasPrecision(18, 2);
        builder.Property(x => x.MinimumSellingPrice).HasPrecision(18, 2);
        builder.Property(x => x.LowStockThreshold).HasPrecision(18, 3);
        builder.Property(x => x.HsnCode).HasMaxLength(8);
        builder.Property(x => x.TaxTreatment).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.GstRate).HasPrecision(5, 2);

        builder.HasIndex(x => x.ProductCode).IsUnique();

        // The foreign key's own index, kept by name: the filtered index below would otherwise replace it.
        builder.HasIndex(x => x.SourceProductId, "IX_Products_SourceProductId");

        // One active pieces product per loose variety, so a transfer knows which one the kg become.
        builder.HasIndex(x => x.SourceProductId, "IX_Products_SourceProductId_ActivePieces")
            .IsUnique()
            .HasFilter("[Kind] = 'Pieces' AND [IsActive] = 1");

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
