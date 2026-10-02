using GoldenPappadam.Domain.OwnShop;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class ShopTransferConfiguration : IEntityTypeConfiguration<ShopTransfer>
{
    public void Configure(EntityTypeBuilder<ShopTransfer> builder)
    {
        builder.ToTable("ShopTransfers", Schemas.OwnShop, table =>
        {
            table.HasCheckConstraint("CK_ShopTransfers_QuantityKg", "[QuantityKg] > 0");
            table.HasCheckConstraint("CK_ShopTransfers_PiecesPerKg", "[PiecesPerKg] > 0");
            table.HasCheckConstraint("CK_ShopTransfers_PiecesReceived", "[PiecesReceived] > 0");
            table.HasCheckConstraint("CK_ShopTransfers_DifferentProducts", "[SourceProductId] <> [PiecesProductId]");
            table.HasCheckConstraint("CK_ShopTransfers_DifferentLocations", "[FromLocationId] <> [ToLocationId]");
        });

        builder.Property(x => x.QuantityKg).HasPrecision(18, 3);
        builder.Property(x => x.PiecesPerKg).HasPrecision(18, 3);
        builder.Property(x => x.PiecesReceived).HasPrecision(18, 3);
        builder.Property(x => x.SourceOnHandBefore).HasPrecision(18, 3);
        builder.Property(x => x.ShopOnHandBefore).HasPrecision(18, 3);
        builder.Property(x => x.Notes).HasMaxLength(300);

        // The same transfer sent twice is recorded once.
        builder.HasIndex(x => x.ClientRequestId).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
        builder.HasIndex(x => x.OccurredAt);

        builder.HasOne(x => x.SourceProduct).WithMany().HasForeignKey(x => x.SourceProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PiecesProduct).WithMany().HasForeignKey(x => x.PiecesProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FromLocation).WithMany().HasForeignKey(x => x.FromLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ToLocation).WithMany().HasForeignKey(x => x.ToLocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ShopSaleConfiguration : IEntityTypeConfiguration<ShopSale>
{
    public void Configure(EntityTypeBuilder<ShopSale> builder)
    {
        builder.ToTable("ShopSales", Schemas.OwnShop, table =>
        {
            table.HasCheckConstraint("CK_ShopSales_Sequence", "[SequenceNumber] > 0");
            table.HasCheckConstraint("CK_ShopSales_TotalAmount", "[TotalAmount] >= 0");

            // Return credit is not money; a counter sale is always paid in money.
            table.HasCheckConstraint("CK_ShopSales_PaymentMethod", "[PaymentMethod] <> 'ReturnCredit'");
        });

        builder.Property(x => x.SaleNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.SeriesCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.FinancialYear).HasMaxLength(7).IsRequired();
        builder.Property(x => x.CustomerName).HasMaxLength(150);
        builder.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(300);
        builder.Property(x => x.CancellationReason).HasMaxLength(300);

        builder.HasIndex(x => x.SaleNumber).IsUnique();
        builder.HasIndex(x => new { x.SeriesCode, x.FinancialYear, x.SequenceNumber }).IsUnique();
        builder.HasIndex(x => x.SaleDate);
        builder.HasIndex(x => new { x.CustomerId, x.SaleDate });
        builder.HasIndex(x => x.ClientRequestId).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");

        builder.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Lines)
            .WithOne(x => x.ShopSale!)
            .HasForeignKey(x => x.ShopSaleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ShopSaleLineConfiguration : IEntityTypeConfiguration<ShopSaleLine>
{
    public void Configure(EntityTypeBuilder<ShopSaleLine> builder)
    {
        builder.ToTable("ShopSaleLines", Schemas.OwnShop, table =>
        {
            table.HasCheckConstraint("CK_ShopSaleLines_Quantity", "[Quantity] > 0");

            // The rate is inside the band the product allowed when it was sold.
            table.HasCheckConstraint(
                "CK_ShopSaleLines_UnitPrice",
                "[MinimumPrice] > 0 AND [UnitPrice] >= [MinimumPrice] AND [UnitPrice] <= [DefaultPrice]");
        });

        builder.Property(x => x.Description).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.DefaultPrice).HasPrecision(18, 2);
        builder.Property(x => x.MinimumPrice).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);

        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => new { x.ShopSaleId, x.LineNumber }).IsUnique();
    }
}
