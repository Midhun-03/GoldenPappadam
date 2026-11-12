using GoldenPappadam.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class ProductPriceChangeConfiguration : IEntityTypeConfiguration<ProductPriceChange>
{
    public void Configure(EntityTypeBuilder<ProductPriceChange> builder)
    {
        builder.ToTable("ProductPriceChanges", Schemas.Inventory);

        builder.Property(x => x.PreviousPrice).HasPrecision(18, 2);
        builder.Property(x => x.NewPrice).HasPrecision(18, 2);

        // "What has this product cost since the phone last synced?" - one product, by time.
        builder.HasIndex(x => new { x.ProductId, x.CreatedAt });

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
