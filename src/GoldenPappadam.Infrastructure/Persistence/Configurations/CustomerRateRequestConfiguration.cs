using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class CustomerRateRequestConfiguration : IEntityTypeConfiguration<CustomerRateRequest>
{
    public void Configure(EntityTypeBuilder<CustomerRateRequest> builder)
    {
        builder.ToTable("CustomerRateRequests", Schemas.Sales, table =>
        {
            table.HasCheckConstraint("CK_CustomerRateRequests_RequestedPrice", "[RequestedPrice] > 0");

            // Decided means someone decided, and when; pending means nobody has yet.
            table.HasCheckConstraint(
                "CK_CustomerRateRequests_Decision",
                "([Status] = 'Pending' AND [DecidedAt] IS NULL) OR ([Status] <> 'Pending' AND [DecidedAt] IS NOT NULL)");
        });

        builder.Property(x => x.PriceWhenRequested).HasPrecision(18, 2);
        builder.Property(x => x.RequestedPrice).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(300);
        builder.Property(x => x.DecisionNote).HasMaxLength(300);

        // One request waiting per customer and product: a newer one replaces it.
        builder.HasIndex(x => new { x.CustomerId, x.ProductId })
            .IsUnique()
            .HasFilter("[Status] = 'Pending'");

        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasIndex(x => x.CreatedBy);

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CustomerRateRequest>()
            .WithMany()
            .HasForeignKey(x => x.ReplacedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
