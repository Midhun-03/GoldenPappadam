using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class InvoiceDocumentConfiguration : IEntityTypeConfiguration<InvoiceDocument>
{
    public void Configure(EntityTypeBuilder<InvoiceDocument> builder)
    {
        builder.ToTable("InvoiceDocuments", Schemas.Sales);

        builder.Property(x => x.StorageKey).HasMaxLength(300).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();

        // One stored copy per invoice: two requests generating at once cannot both win.
        builder.HasIndex(x => x.InvoiceId).IsUnique();

        builder.HasOne(x => x.Invoice)
            .WithMany()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
