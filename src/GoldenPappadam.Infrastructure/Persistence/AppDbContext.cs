using System.Reflection;
using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerPrice> CustomerPrices => Set<CustomerPrice>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();

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
}
