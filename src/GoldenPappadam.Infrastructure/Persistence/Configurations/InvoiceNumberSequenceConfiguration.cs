using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class InvoiceNumberSequenceConfiguration : IEntityTypeConfiguration<InvoiceNumberSequence>
{
    public void Configure(EntityTypeBuilder<InvoiceNumberSequence> builder)
    {
        builder.ToTable("InvoiceNumberSequences", Schemas.Sales, table =>
            table.HasCheckConstraint("CK_InvoiceNumberSequences_LastNumber", "[LastNumber] >= 0"));

        builder.Property(x => x.SeriesCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.FinancialYear).HasMaxLength(7).IsRequired();

        // One counter per series per year. Also what stops two first-invoices-of-the-year from
        // each creating their own counter.
        builder.HasIndex(x => new { x.SeriesCode, x.FinancialYear }).IsUnique();
    }
}
