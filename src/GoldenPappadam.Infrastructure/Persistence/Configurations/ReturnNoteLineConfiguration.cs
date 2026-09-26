using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class ReturnNoteLineConfiguration : IEntityTypeConfiguration<ReturnNoteLine>
{
    public void Configure(EntityTypeBuilder<ReturnNoteLine> builder)
    {
        builder.ToTable("ReturnNoteLines", Schemas.Sales, table =>
        {
            table.HasCheckConstraint("CK_ReturnNoteLines_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_ReturnNoteLines_UnitRate", "[UnitRate] >= 0");
        });

        builder.Property(x => x.Description).HasMaxLength(150).IsRequired();
        builder.Property(x => x.UnitCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.Reason).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.UnitRate).HasPrecision(18, 2);
        builder.Property(x => x.Value).HasPrecision(18, 2);

        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => new { x.ReturnNoteId, x.LineNumber }).IsUnique();
    }
}
