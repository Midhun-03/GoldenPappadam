using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class CustomerPriceConfiguration : IEntityTypeConfiguration<CustomerPrice>
{
    public void Configure(EntityTypeBuilder<CustomerPrice> builder)
    {
        builder.ToTable("CustomerPrices", Schemas.Sales, table =>
            table.HasCheckConstraint("CK_CustomerPrices_UnitPrice", "[UnitPrice] >= 0"));

        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);

        // One arrangement per shop per product. A second row would make "the price" ambiguous,
        // which is exactly what a salesperson must never have to think about.
        builder.HasIndex(x => new { x.CustomerId, x.ProductId }).IsUnique();

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
