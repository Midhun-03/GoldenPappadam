using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class InvoiceEmailLogConfiguration : IEntityTypeConfiguration<InvoiceEmailLog>
{
    public void Configure(EntityTypeBuilder<InvoiceEmailLog> builder)
    {
        builder.ToTable("InvoiceEmailLogs", Schemas.Sales);

        builder.Property(x => x.Recipient).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.ErrorMessage).HasMaxLength(500);

        builder.HasIndex(x => new { x.InvoiceId, x.CreatedAt });

        builder.HasOne(x => x.Invoice)
            .WithMany()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
