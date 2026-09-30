using System.Reflection;
using GoldenPappadam.Domain.Accounting;
using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.FieldSales;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Domain.Staff;
using GoldenPappadam.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace GoldenPappadam.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<UnitOfMeasure> UnitOfMeasures => Set<UnitOfMeasure>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockLocation> StockLocations => Set<StockLocation>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<PackingEntry> PackingEntries => Set<PackingEntry>();
    public DbSet<RepackEntry> RepackEntries => Set<RepackEntry>();

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerBranch> CustomerBranches => Set<CustomerBranch>();
    public DbSet<CustomerPrice> CustomerPrices => Set<CustomerPrice>();
    public DbSet<CustomerPriceChange> CustomerPriceChanges => Set<CustomerPriceChange>();
    public DbSet<CustomerRateRequest> CustomerRateRequests => Set<CustomerRateRequest>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<InvoiceSettings> InvoiceSettings => Set<InvoiceSettings>();
    public DbSet<InvoiceNumberSequence> InvoiceNumberSequences => Set<InvoiceNumberSequence>();
    public DbSet<InvoiceDocument> InvoiceDocuments => Set<InvoiceDocument>();
    public DbSet<InvoiceEmailLog> InvoiceEmailLogs => Set<InvoiceEmailLog>();
    public DbSet<ReturnNote> ReturnNotes => Set<ReturnNote>();
    public DbSet<ReturnNoteLine> ReturnNoteLines => Set<ReturnNoteLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();

    public DbSet<Device> Devices => Set<Device>();
    public DbSet<ShopVisit> ShopVisits => Set<ShopVisit>();
    public DbSet<SyncSubmission> SyncSubmissions => Set<SyncSubmission>();
    public DbSet<StockRequest> StockRequests => Set<StockRequest>();
    public DbSet<StockRequestLine> StockRequestLines => Set<StockRequestLine>();
    public DbSet<VanLoad> VanLoads => Set<VanLoad>();
    public DbSet<VanLoadLine> VanLoadLines => Set<VanLoadLine>();

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeWageRate> EmployeeWageRates => Set<EmployeeWageRate>();
    public DbSet<AttendanceStatus> AttendanceStatuses => Set<AttendanceStatus>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<WagePayment> WagePayments => Set<WagePayment>();
    public DbSet<WagePaymentLine> WagePaymentLines => Set<WagePaymentLine>();

    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseChange> ExpenseChanges => Set<ExpenseChange>();

    /// <summary>
    /// SQL Server's datetime2 does not remember that a value is UTC, so EF hands it back as
    /// "unspecified" and clients read it as local time. This stamps every DateTime as UTC on the
    /// way out, and converts to UTC on the way in.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        base.ConfigureConventions(builder);

        builder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        builder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>().ToTable("Users", Schemas.Identity);
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles", Schemas.Identity);
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles", Schemas.Identity);
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims", Schemas.Identity);
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins", Schemas.Identity);
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens", Schemas.Identity);
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims", Schemas.Identity);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public override int SaveChanges()
    {
        ApplyAuditRules();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditRules();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Fills the audit fields in one place, and enforces that records which are not
    /// <see cref="AuditableEntity"/> - stock movements, packing entries - are never edited or deleted.
    /// </summary>
    private void ApplyAuditRules()
    {
        var now = DateTime.UtcNow;
        var userId = currentUser.UserId;

        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy ??= userId;
                    break;

                case EntityState.Modified:
                    if (entry.Entity is not AuditableEntity auditable)
                    {
                        throw new InvalidOperationException(
                            $"{entry.Entity.GetType().Name} records cannot be edited. " +
                            "Correct them with a new record instead.");
                    }

                    if (entry.Entity is Invoice)
                    {
                        EnsureOnlyAllowedChanges(entry, Invoice.PropertiesEditableAfterFinalization, "finalized invoice");
                    }
                    else if (entry.Entity is ReturnNote)
                    {
                        EnsureOnlyAllowedChanges(entry, ReturnNote.PropertiesEditableAfterRecording, "recorded return");
                    }
                    else if (entry.Entity is CustomerRateRequest)
                    {
                        EnsureOnlyAllowedChanges(entry, CustomerRateRequest.DecisionProperties, "rate-change request");
                    }
                    else if (entry.Entity is WagePayment)
                    {
                        EnsureOnlyAllowedChanges(entry, WagePayment.PropertiesEditableAfterPayment, "wage payment");
                    }
                    else if (entry.Entity is Expense { WagePaymentId: not null })
                    {
                        EnsureOnlyAllowedChanges(entry, Expense.PropertiesEditableOnWageExpense, "wage expense");
                    }

                    auditable.UpdatedAt = now;
                    auditable.UpdatedBy = userId;
                    entry.Property(nameof(Entity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(Entity.CreatedBy)).IsModified = false;
                    break;

                case EntityState.Deleted:
                    if (entry.Entity is not AuditableEntity)
                    {
                        throw new InvalidOperationException(
                            $"{entry.Entity.GetType().Name} records cannot be deleted. " +
                            "Correct them with a new record instead.");
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Invoices, return notes and wage payments are documents. The only changes they may ever receive
    /// are the ones their type allows - being cancelled, or a pending return being settled; anything else, an
    /// amount, a customer, a date, is refused here, whichever code path tried it, rather than
    /// trusting every service to remember.
    /// </summary>
    private static void EnsureOnlyAllowedChanges(EntityEntry entry, IReadOnlySet<string> allowed, string what)
    {
        var changed = entry.Properties
            .Where(p => p.IsModified && !allowed.Contains(p.Metadata.Name))
            .Select(p => p.Metadata.Name)
            .ToList();

        if (changed.Count > 0)
        {
            throw new InvalidOperationException(
                $"A {what} cannot be changed ({string.Join(", ", changed)}). Cancel it and record a new one instead.");
        }
    }
}
