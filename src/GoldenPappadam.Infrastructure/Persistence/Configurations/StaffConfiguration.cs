using GoldenPappadam.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees", Schemas.Staff);

        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Designation).HasMaxLength(100);
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.Property(x => x.Address).HasMaxLength(300);

        // Not unique: two people can share a name. The screens show the designation beside it.
        builder.HasIndex(x => x.Name);

        builder.HasMany(x => x.WageRates)
            .WithOne(x => x.Employee!)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class EmployeeWageRateConfiguration : IEntityTypeConfiguration<EmployeeWageRate>
{
    public void Configure(EntityTypeBuilder<EmployeeWageRate> builder)
    {
        builder.ToTable("EmployeeWageRates", Schemas.Staff, table =>
            table.HasCheckConstraint("CK_EmployeeWageRates_DailyWage", "[DailyWage] > 0"));

        builder.Property(x => x.DailyWage).HasPrecision(18, 2);

        // The rate in effect on a day: the latest start on or before it.
        builder.HasIndex(x => new { x.EmployeeId, x.EffectiveFrom });
    }
}

public class AttendanceStatusConfiguration : IEntityTypeConfiguration<AttendanceStatus>
{
    /// <summary>Fixed ids and timestamp: seed data must stay identical across migrations.</summary>
    private static readonly DateTime SeededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<AttendanceStatus> builder)
    {
        builder.ToTable("AttendanceStatuses", Schemas.Staff, table =>
            table.HasCheckConstraint("CK_AttendanceStatuses_DayFraction", "[DayFraction] >= 0 AND [DayFraction] <= 1"));

        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.Property(x => x.DayFraction).HasPrecision(4, 2);

        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasData(
            new AttendanceStatus
            {
                Id = KnownAttendanceStatuses.PresentId, Code = "Present", Name = "Present",
                DayFraction = 1m, SortOrder = 1, CreatedAt = SeededAt
            },
            new AttendanceStatus
            {
                Id = KnownAttendanceStatuses.HalfDayId, Code = "HalfDay", Name = "Half day",
                DayFraction = 0.5m, SortOrder = 2, CreatedAt = SeededAt
            },
            new AttendanceStatus
            {
                Id = KnownAttendanceStatuses.AbsentId, Code = "Absent", Name = "Absent",
                DayFraction = 0m, SortOrder = 3, CreatedAt = SeededAt
            },
            new AttendanceStatus
            {
                Id = KnownAttendanceStatuses.LeaveId, Code = "Leave", Name = "Leave",
                DayFraction = 0m, SortOrder = 4, CreatedAt = SeededAt
            });
    }
}

public class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("AttendanceRecords", Schemas.Staff);

        // One record per employee per day; it also serves "this employee's week".
        builder.HasIndex(x => new { x.EmployeeId, x.WorkDate }).IsUnique();
        builder.HasIndex(x => x.WorkDate);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AttendanceStatus)
            .WithMany()
            .HasForeignKey(x => x.AttendanceStatusId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WagePaymentConfiguration : IEntityTypeConfiguration<WagePayment>
{
    public void Configure(EntityTypeBuilder<WagePayment> builder)
    {
        builder.ToTable("WagePayments", Schemas.Staff, table =>
        {
            table.HasCheckConstraint("CK_WagePayments_Amount", "[Amount] >= 0");
            table.HasCheckConstraint("CK_WagePayments_CarriedForward", "[CarriedForward] <= 0");
        });

        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Reference).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(300);
        builder.Property(x => x.DaysWorked).HasPrecision(6, 2);
        builder.Property(x => x.WorkAmount).HasPrecision(18, 2);
        builder.Property(x => x.AdjustmentAmount).HasPrecision(18, 2);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.CarriedForward).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.CancellationReason).HasMaxLength(300);

        // The guard against paying a week twice: at most one standing payment per employee per week.
        // A cancelled one stays in the table and frees the week to be paid again.
        builder.HasIndex(x => new { x.EmployeeId, x.PeriodStart })
            .IsUnique()
            .HasFilter("[Status] = 'Paid'");
        builder.HasIndex(x => x.PeriodStart);
        builder.HasIndex(x => x.PaymentDate);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Lines)
            .WithOne(x => x.WagePayment!)
            .HasForeignKey(x => x.WagePaymentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WagePaymentLineConfiguration : IEntityTypeConfiguration<WagePaymentLine>
{
    public void Configure(EntityTypeBuilder<WagePaymentLine> builder)
    {
        builder.ToTable("WagePaymentLines", Schemas.Staff);

        builder.Property(x => x.LineType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.StatusName).HasMaxLength(50);
        builder.Property(x => x.PreviousStatusName).HasMaxLength(50);
        builder.Property(x => x.DayFraction).HasPrecision(4, 2);
        builder.Property(x => x.PreviousDayFraction).HasPrecision(4, 2);
        builder.Property(x => x.DailyWage).HasPrecision(18, 2);
        builder.Property(x => x.Amount).HasPrecision(18, 2);

        builder.HasIndex(x => x.SourceWagePaymentId);

        builder.HasOne(x => x.SourceWagePayment)
            .WithMany()
            .HasForeignKey(x => x.SourceWagePaymentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
