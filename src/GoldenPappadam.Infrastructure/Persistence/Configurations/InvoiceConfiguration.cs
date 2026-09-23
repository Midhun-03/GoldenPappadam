using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices", Schemas.Sales, table =>
        {
            // The grand total is exactly its parts. With tax-inclusive rates the tax sits inside
            // SubTotal - Discount; with tax-exclusive rates it is added on top. Either way this holds.
            table.HasCheckConstraint(
                "CK_Invoices_Total",
                "[TotalAmount] = [TaxableAmount] + [CgstAmount] + [SgstAmount] + [IgstAmount] + [CessAmount] + [RoundOff]");
            table.HasCheckConstraint(
                "CK_Invoices_Discount",
                "[DiscountAmount] >= 0 AND [DiscountAmount] <= [SubTotal]");

            // Intra-state is CGST + SGST, inter-state is IGST, and never both on one bill.
            table.HasCheckConstraint(
                "CK_Invoices_TaxSplit",
                "([IsInterState] = 1 AND [CgstAmount] = 0 AND [SgstAmount] = 0) " +
                "OR ([IsInterState] = 0 AND [IgstAmount] = 0)");
            table.HasCheckConstraint("CK_Invoices_Sequence", "[SequenceNumber] > 0");
        });

        builder.Property(x => x.InvoiceNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.SeriesCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.FinancialYear).HasMaxLength(7).IsRequired();
        builder.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(x => x.SupplierName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.SupplierAddress).HasMaxLength(300);
        builder.Property(x => x.SupplierGstin).HasMaxLength(15);
        builder.Property(x => x.SupplierStateCode).HasMaxLength(2).IsRequired();

        builder.Property(x => x.CustomerName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.CustomerAddress).HasMaxLength(300);
        builder.Property(x => x.CustomerPhone).HasMaxLength(20);
        builder.Property(x => x.CustomerGstin).HasMaxLength(15);
        builder.Property(x => x.CustomerStateCode).HasMaxLength(2);

        builder.Property(x => x.BranchName).HasMaxLength(150);
        builder.Property(x => x.BranchAddress).HasMaxLength(300);
        builder.Property(x => x.BranchPhone).HasMaxLength(20);
        builder.Property(x => x.BranchGstin).HasMaxLength(15);
        builder.Property(x => x.BranchStateCode).HasMaxLength(2);

        builder.Property(x => x.PlaceOfSupplyStateCode).HasMaxLength(2);

        builder.Property(x => x.SubTotal).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.TaxableAmount).HasPrecision(18, 2);
        builder.Property(x => x.CgstAmount).HasPrecision(18, 2);
        builder.Property(x => x.SgstAmount).HasPrecision(18, 2);
        builder.Property(x => x.IgstAmount).HasPrecision(18, 2);
        builder.Property(x => x.CessAmount).HasPrecision(18, 2);
        builder.Property(x => x.RoundOff).HasPrecision(18, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(300);
        builder.Property(x => x.CancellationReason).HasMaxLength(300);

        // Two guarantees that no number is ever issued twice: the formatted number, and the
        // series/year/position it is built from. Neither relies on application code.
        builder.HasIndex(x => x.InvoiceNumber).IsUnique();
        builder.HasIndex(x => new { x.SeriesCode, x.FinancialYear, x.SequenceNumber }).IsUnique();
        builder.HasIndex(x => new { x.CustomerId, x.InvoiceDate });
        builder.HasIndex(x => x.InvoiceDate);

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Lines)
            .WithOne(x => x.Invoice!)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
