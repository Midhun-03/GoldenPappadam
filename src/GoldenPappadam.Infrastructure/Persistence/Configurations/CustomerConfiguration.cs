using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers", Schemas.Sales);

        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.ContactPerson).HasMaxLength(100);
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.Notes).HasMaxLength(300);
        builder.Property(x => x.OpeningBalance).HasPrecision(18, 2);

        builder.HasIndex(x => x.Name);
    }
}
