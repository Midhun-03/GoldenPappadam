using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.ToTable("PaymentAllocations", Schemas.Sales, table =>
            table.HasCheckConstraint("CK_PaymentAllocations_Amount", "[Amount] > 0"));

        builder.Property(x => x.Amount).HasPrecision(18, 2);

        builder.HasOne(x => x.Invoice)
            .WithMany()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        // One row per payment and invoice pair: amounts are combined, not listed twice.
        builder.HasIndex(x => new { x.PaymentId, x.InvoiceId }).IsUnique();
        builder.HasIndex(x => x.InvoiceId);
    }
}
