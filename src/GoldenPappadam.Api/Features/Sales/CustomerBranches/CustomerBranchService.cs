using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Sales.CustomerBranches;

/// <summary>
/// The physical shops under a multi-branch customer. Pricing is deliberately untouched here -
/// <see cref="CustomerPrices.CustomerPriceService"/> keys on the parent customer, so every branch
/// inherits the one arrangement instead of getting its own.
/// </summary>
public class CustomerBranchService(AppDbContext db)
{
    public async Task<IReadOnlyList<CustomerBranchDto>> GetForCustomerAsync(
        Guid customerId,
        bool includeInactive,
        CancellationToken ct)
    {
        await EnsureCustomerExistsAsync(customerId, ct);

        return await db.CustomerBranches
            .Where(b => b.CustomerId == customerId)
            .Where(b => includeInactive || b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => ToDto(b))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Adding a branch is what makes a customer multi-branch: it sets the flag rather than
    /// requiring it be set first, so the office cannot get the two out of sync.
    /// </summary>
    public async Task<CustomerBranchDto> CreateAsync(
        Guid customerId,
        SaveCustomerBranchRequest request,
        CancellationToken ct,
        Guid? id = null)
    {
        var customer = await EnsureCustomerExistsAsync(customerId, ct);

        if (!customer.IsActive)
        {
            throw new DomainException($"Customer '{customer.Name}' is not active.");
        }

        await EnsureNameIsFreeAsync(customerId, request.Name, null, ct);

        var branch = new CustomerBranch
        {
            Id = id ?? Guid.Empty,
            CustomerId = customerId,
            Name = request.Name.Trim(),
            Location = Clean(request.Location),
            Address = Clean(request.Address),
            Phone = Clean(request.Phone),
            ContactPerson = Clean(request.ContactPerson)
        };

        db.CustomerBranches.Add(branch);
        customer.HasMultipleBranches = true;

        await db.SaveChangesAsync(ct);

        return ToDto(branch);
    }

    public async Task<CustomerBranchDto> UpdateAsync(
        Guid customerId,
        Guid branchId,
        SaveCustomerBranchRequest request,
        CancellationToken ct)
    {
        var branch = await FindBranchAsync(customerId, branchId, ct);

        await EnsureNameIsFreeAsync(customerId, request.Name, branchId, ct);

        branch.Name = request.Name.Trim();
        branch.Location = Clean(request.Location);
        branch.Address = Clean(request.Address);
        branch.Phone = Clean(request.Phone);
        branch.ContactPerson = Clean(request.ContactPerson);

        await db.SaveChangesAsync(ct);

        return ToDto(branch);
    }

    /// <summary>Branches are deactivated, never deleted, because bills already made refer to them.</summary>
    public async Task<CustomerBranchDto> SetActiveAsync(Guid customerId, Guid branchId, bool isActive, CancellationToken ct)
    {
        var branch = await FindBranchAsync(customerId, branchId, ct);

        branch.IsActive = isActive;
        await db.SaveChangesAsync(ct);

        return ToDto(branch);
    }

    private async Task<CustomerBranch> FindBranchAsync(Guid customerId, Guid branchId, CancellationToken ct) =>
        await db.CustomerBranches.FirstOrDefaultAsync(b => b.Id == branchId && b.CustomerId == customerId, ct)
        ?? throw new NotFoundException("Branch");

    private async Task<Customer> EnsureCustomerExistsAsync(Guid customerId, CancellationToken ct) =>
        await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct)
        ?? throw new NotFoundException("Customer");

    private async Task EnsureNameIsFreeAsync(Guid customerId, string name, Guid? exceptId, CancellationToken ct)
    {
        var trimmed = name.Trim();
        var taken = await db.CustomerBranches.AnyAsync(
            b => b.CustomerId == customerId && b.Name == trimmed && (exceptId == null || b.Id != exceptId), ct);

        if (taken)
        {
            throw new DomainException($"A branch named '{trimmed}' already exists for this customer.");
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CustomerBranchDto ToDto(CustomerBranch branch) => new(
        branch.Id, branch.CustomerId, branch.Name, branch.Location, branch.Address,
        branch.Phone, branch.ContactPerson, branch.IsActive);
}
