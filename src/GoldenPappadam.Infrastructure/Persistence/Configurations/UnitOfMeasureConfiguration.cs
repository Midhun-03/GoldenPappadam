using GoldenPappadam.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    /// <summary>Fixed ids and timestamp: seed data must stay identical across migrations.</summary>
    private static readonly DateTime SeededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<UnitOfMeasure> builder)
    {
        builder.ToTable("UnitOfMeasures", Schemas.Inventory);

        builder.Property(x => x.Code).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasData(
            new UnitOfMeasure
            {
                Id = Guid.Parse("2a9f3f9e-0f01-4a1e-9f7a-0b1a0a000001"),
                Code = "KG",
                Name = "Kilogram",
                CreatedAt = SeededAt
            },
            new UnitOfMeasure
            {
                Id = Guid.Parse("2a9f3f9e-0f01-4a1e-9f7a-0b1a0a000002"),
                Code = "PCS",
                Name = "Piece",
                CreatedAt = SeededAt
            },
            new UnitOfMeasure
            {
                Id = Guid.Parse("2a9f3f9e-0f01-4a1e-9f7a-0b1a0a000003"),
                Code = "PKT",
                Name = "Packet",
                CreatedAt = SeededAt
            },
            new UnitOfMeasure
            {
                Id = Guid.Parse("2a9f3f9e-0f01-4a1e-9f7a-0b1a0a000004"),
                Code = "BOX",
                Name = "Box",
                CreatedAt = SeededAt
            });
    }
}
