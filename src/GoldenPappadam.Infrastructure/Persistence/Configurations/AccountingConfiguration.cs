using GoldenPappadam.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoldenPappadam.Infrastructure.Persistence.Configurations;

public class ExpenseCategoryConfiguration : IEntityTypeConfiguration<ExpenseCategory>
{
    /// <summary>Fixed ids and timestamp: seed data must stay identical across migrations.</summary>
    private static readonly DateTime SeededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<ExpenseCategory> builder)
    {
        builder.ToTable("ExpenseCategories", Schemas.Accounting);

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => x.Name).IsUnique();

        builder.HasData(KnownExpenseCategories.Seeded.Select(c => new ExpenseCategory
        {
            Id = c.Id,
            Name = c.Name,
            CreatedAt = SeededAt
        }));
    }
}

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses", Schemas.Accounting, table =>
            table.HasCheckConstraint("CK_Expenses_Amount", "[Amount] > 0"));

        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Description).HasMaxLength(300);
        builder.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Reference).HasMaxLength(100);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasIndex(x => x.ExpenseDate);
        builder.HasIndex(x => new { x.CategoryId, x.ExpenseDate });

        // One expense per wage payment, so the same wages can never be counted twice.
        builder.HasIndex(x => x.WagePaymentId).IsUnique().HasFilter("[WagePaymentId] IS NOT NULL");

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.WagePayment)
            .WithMany()
            .HasForeignKey(x => x.WagePaymentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ExpenseChangeConfiguration : IEntityTypeConfiguration<ExpenseChange>
{
    public void Configure(EntityTypeBuilder<ExpenseChange> builder)
    {
        builder.ToTable("ExpenseChanges", Schemas.Accounting);

        builder.Property(x => x.ChangeType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Description).HasMaxLength(300);
        builder.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Reference).HasMaxLength(100);
        builder.Property(x => x.Reason).HasMaxLength(300);

        builder.HasIndex(x => new { x.ExpenseId, x.CreatedAt });

        builder.HasOne(x => x.Expense)
            .WithMany()
            .HasForeignKey(x => x.ExpenseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
