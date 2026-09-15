using GoldenPappadam.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class StockLocationConfiguration : IEntityTypeConfiguration<StockLocation>
{
    /// <summary>Fixed ids and timestamp: seed data must stay identical across migrations.</summary>
    private static readonly DateTime SeededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<StockLocation> builder)
    {
        builder.ToTable("StockLocations", Schemas.Inventory);

        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasData(
            new StockLocation
            {
                Id = KnownStockLocations.MainWarehouseId,
                Code = "MAIN",
                Name = "Main warehouse",
                Kind = StockLocationKind.Warehouse,
                CreatedAt = SeededAt
            },
            new StockLocation
            {
                Id = KnownStockLocations.FirstVanId,
                Code = "VAN-1",
                Name = "Sales van 1",
                Kind = StockLocationKind.Van,
                CreatedAt = SeededAt
            });
    }
}
