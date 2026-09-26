using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class ReturnNoteConfiguration : IEntityTypeConfiguration<ReturnNote>
{
    public void Configure(EntityTypeBuilder<ReturnNote> builder)
    {
        builder.ToTable("ReturnNotes", Schemas.Sales, table =>
        {
            table.HasCheckConstraint("CK_ReturnNotes_Sequence", "[SequenceNumber] > 0");
            table.HasCheckConstraint("CK_ReturnNotes_Value", "[Value] >= 0");

            // A credit is exactly what the payment carries; any other settlement credits nothing.
            table.HasCheckConstraint(
                "CK_ReturnNotes_Credit",
                "([Settlement] = 'Credit' AND [CreditAmount] > 0 AND [CreditPaymentId] IS NOT NULL) " +
                "OR ([Settlement] <> 'Credit' AND [CreditAmount] = 0 AND [CreditPaymentId] IS NULL)");
        });

        builder.Property(x => x.ReturnNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.SeriesCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.FinancialYear).HasMaxLength(7).IsRequired();
        builder.Property(x => x.CustomerName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.BranchName).HasMaxLength(150);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Settlement).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Value).HasPrecision(18, 2);
        builder.Property(x => x.CreditAmount).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(300);
        builder.Property(x => x.CancellationReason).HasMaxLength(300);

        builder.HasIndex(x => x.ReturnNumber).IsUnique();
        builder.HasIndex(x => new { x.SeriesCode, x.FinancialYear, x.SequenceNumber }).IsUnique();
        builder.HasIndex(x => new { x.CustomerId, x.ReturnDate });
        builder.HasIndex(x => x.ReturnDate);
        builder.HasIndex(x => x.CreditPaymentId).IsUnique().HasFilter("[CreditPaymentId] IS NOT NULL");

        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreditPayment).WithMany().HasForeignKey(x => x.CreditPaymentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Lines)
            .WithOne(x => x.ReturnNote!)
            .HasForeignKey(x => x.ReturnNoteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
