using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class CustomerPriceChangeConfiguration : IEntityTypeConfiguration<CustomerPriceChange>
{
    public void Configure(EntityTypeBuilder<CustomerPriceChange> builder)
    {
        builder.ToTable("CustomerPriceChanges", Schemas.Sales);

        builder.Property(x => x.PreviousPrice).HasPrecision(18, 2);
        builder.Property(x => x.NewPrice).HasPrecision(18, 2);

        // One customer's history, newest first, and the office's "what changed lately" across all.
        builder.HasIndex(x => new { x.CustomerId, x.CreatedAt });
        builder.HasIndex(x => x.CreatedAt);

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
