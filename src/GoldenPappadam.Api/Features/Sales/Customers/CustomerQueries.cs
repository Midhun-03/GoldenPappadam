using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Identity;
using GoldenPappadam.Infrastructure.Persistence;

namespace GoldenPappadam.Api.Features.Sales.Customers;

public static class CustomerQueries
{
    /// <summary>
    /// Customer balance = opening balance + issued bills − money received.
    /// It uses the payment amount rather than its allocations, so money on account counts
    /// straight away and the balance is never overstated.
    /// </summary>
    public static IQueryable<CustomerDto> Project(IQueryable<Customer> customers, AppDbContext db) =>
        customers.Select(c => new CustomerDto(
            c.Id,
            c.Name,
            c.ContactPerson,
            c.Phone,
            c.Address,
            c.OpeningBalance,
            c.OpeningBalance
            + (db.Invoices
                   .Where(i => i.CustomerId == c.Id && i.Status == InvoiceStatus.Issued)
                   .Sum(i => (decimal?)i.TotalAmount) ?? 0m)
            - (db.Payments
                   .Where(p => p.CustomerId == c.Id)
                   .Sum(p => (decimal?)p.Amount) ?? 0m),
            c.Notes,
            c.IsActive,
            c.HasMultipleBranches,
            db.CustomerBranches.Count(b => b.CustomerId == c.Id && b.IsActive),
            db.Users.Where(u => u.Id == c.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
            db.UserRoles.Any(ur =>
                ur.UserId == c.CreatedBy && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Salesperson))));
}
