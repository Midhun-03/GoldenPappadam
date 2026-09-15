using GoldenPappadam.Domain.FieldSales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class VanLoadConfiguration : IEntityTypeConfiguration<VanLoad>
{
    public void Configure(EntityTypeBuilder<VanLoad> builder)
    {
        builder.ToTable("VanLoads", Schemas.FieldSales, table =>
            table.HasCheckConstraint("CK_VanLoads_Ends", "[VanLocationId] <> [WarehouseLocationId]"));

        builder.Property(x => x.Direction).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(300);

        builder.HasOne(x => x.VanLocation)
            .WithMany()
            .HasForeignKey(x => x.VanLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.WarehouseLocation)
            .WithMany()
            .HasForeignKey(x => x.WarehouseLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Lines)
            .WithOne(x => x.VanLoad!)
            .HasForeignKey(x => x.VanLoadId)
            .OnDelete(DeleteBehavior.Cascade);

        // "What happened with this van on this day?" - the reconciliation's question.
        builder.HasIndex(x => new { x.VanLocationId, x.BusinessDate });
    }
}

public class VanLoadLineConfiguration : IEntityTypeConfiguration<VanLoadLine>
{
    public void Configure(EntityTypeBuilder<VanLoadLine> builder)
    {
        builder.ToTable("VanLoadLines", Schemas.FieldSales, table =>
            table.HasCheckConstraint("CK_VanLoadLines_Quantity", "[Quantity] > 0"));

        builder.Property(x => x.Quantity).HasPrecision(18, 3);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProductId);
    }
}
