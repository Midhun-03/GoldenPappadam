using GoldenPappadam.Domain.FieldSales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("Devices", Schemas.FieldSales);

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Platform).HasMaxLength(30).IsRequired();

        builder.HasOne(x => x.Location)
            .WithMany()
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.UserId);
    }
}

public class ShopVisitConfiguration : IEntityTypeConfiguration<ShopVisit>
{
    public void Configure(EntityTypeBuilder<ShopVisit> builder)
    {
        builder.ToTable("ShopVisits", Schemas.FieldSales);

        builder.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(300);

        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.VisitedAt);
        builder.HasIndex(x => new { x.CustomerId, x.VisitedAt });
    }
}

public class SyncSubmissionConfiguration : IEntityTypeConfiguration<SyncSubmission>
{
    public void Configure(EntityTypeBuilder<SyncSubmission> builder)
    {
        builder.ToTable("SyncSubmissions", Schemas.FieldSales);

        builder.Property(x => x.SubmissionType).HasConversion<string>().HasMaxLength(20).IsRequired();

        // The whole idempotency guarantee, in one line. A retry collides here rather than
        // creating a second bill, and the database enforces it even if two syncs race.
        builder.HasIndex(x => x.ClientRequestId).IsUnique();

        builder.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        // "What did this phone send today?" - the admin day screen's question.
        builder.HasIndex(x => new { x.DeviceId, x.ReceivedAt });
        builder.HasIndex(x => x.CreatedRecordId);
    }
}
