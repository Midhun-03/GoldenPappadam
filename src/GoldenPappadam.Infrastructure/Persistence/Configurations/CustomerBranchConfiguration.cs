using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class CustomerBranchConfiguration : IEntityTypeConfiguration<CustomerBranch>
{
    public void Configure(EntityTypeBuilder<CustomerBranch> builder)
    {
        builder.ToTable("CustomerBranches", Schemas.Sales);

        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Location).HasMaxLength(100);
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.Property(x => x.ContactPerson).HasMaxLength(100);
        builder.Property(x => x.Gstin).HasMaxLength(15);
        builder.Property(x => x.StateCode).HasMaxLength(2);

        // One branch name per customer, so "Kundara" cannot be added twice under the same shop.
        builder.HasIndex(x => new { x.CustomerId, x.Name }).IsUnique();

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.Branches)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
