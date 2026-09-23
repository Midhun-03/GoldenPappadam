using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class InvoiceSettingsConfiguration : IEntityTypeConfiguration<InvoiceSettings>
{
    /// <summary>Fixed timestamp: seed data must stay identical across migrations.</summary>
    private static readonly DateTime SeededAt = new(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<InvoiceSettings> builder)
    {
        builder.ToTable("InvoiceSettings", Schemas.Sales);

        builder.Property(x => x.LegalName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.Gstin).HasMaxLength(15);
        builder.Property(x => x.StateCode).HasMaxLength(2).IsRequired();
        builder.Property(x => x.SeriesCode).HasMaxLength(3).IsRequired();
        builder.Property(x => x.PaymentTerms).HasMaxLength(200);
        builder.Property(x => x.BankDetails).HasMaxLength(500);
        builder.Property(x => x.TermsAndConditions).HasMaxLength(1000);

        // No GSTIN is seeded: GST stays off until someone enters the real number.
        builder.HasData(new InvoiceSettings
        {
            Id = InvoiceSettings.SingletonId,
            LegalName = "Golden Pappadam",
            Address = "Kundara, Kollam, Kerala",
            StateCode = IndianStates.KeralaCode,
            SeriesCode = "GP",
            PricesIncludeTax = true,
            RoundToNearestRupee = false,
            CreatedAt = SeededAt
        });
    }
}
