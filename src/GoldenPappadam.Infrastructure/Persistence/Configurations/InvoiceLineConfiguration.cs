using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("InvoiceLines", Schemas.Sales, table =>
        {
            table.HasCheckConstraint("CK_InvoiceLines_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_InvoiceLines_UnitPrice", "[UnitPrice] >= 0");
            table.HasCheckConstraint("CK_InvoiceLines_Discount", "[DiscountAmount] >= 0 AND [DiscountAmount] <= [LineTotal]");
            table.HasCheckConstraint("CK_InvoiceLines_GstRate", "[GstRate] >= 0 AND [GstRate] <= 100");
        });

        builder.Property(x => x.Description).HasMaxLength(150).IsRequired();
        builder.Property(x => x.UnitCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.HsnCode).HasMaxLength(8);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.TaxTreatment).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.GstRate).HasPrecision(5, 2);
        builder.Property(x => x.TaxableValue).HasPrecision(18, 2);
        builder.Property(x => x.CgstAmount).HasPrecision(18, 2);
        builder.Property(x => x.SgstAmount).HasPrecision(18, 2);
        builder.Property(x => x.IgstAmount).HasPrecision(18, 2);
        builder.Property(x => x.CessAmount).HasPrecision(18, 2);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => new { x.InvoiceId, x.LineNumber }).IsUnique();
    }
}
