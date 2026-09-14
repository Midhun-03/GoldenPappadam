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
        });

        builder.Property(x => x.Description).HasMaxLength(150).IsRequired();
        builder.Property(x => x.UnitCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProductId);
    }
}
