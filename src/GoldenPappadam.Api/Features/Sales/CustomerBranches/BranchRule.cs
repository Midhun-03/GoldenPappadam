using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Sales.CustomerBranches;

/// <summary>
/// Which branch a document is for. A multi-branch customer must name the shop; a plain customer must
/// not, since it has no branch to point at. This is what keeps a bill - or a return - from ever
/// landing on the wrong Danya Supermarket branch, or on a branch of a different shop entirely.
/// </summary>
public static class BranchRule
{
    public static async Task<CustomerBranch?> ResolveAsync(
        AppDbContext db,
        Customer customer,
        Guid? requestedBranchId,
        CancellationToken ct)
    {
        if (!customer.HasMultipleBranches)
        {
            return null;
        }

        if (requestedBranchId is null)
        {
            throw new DomainException("Please select a branch before continuing.");
        }

        var branch = await db.CustomerBranches
                         .AsNoTracking()
                         .FirstOrDefaultAsync(b => b.Id == requestedBranchId && b.CustomerId == customer.Id, ct)
                     ?? throw new NotFoundException("Branch");

        if (!branch.IsActive)
        {
            throw new DomainException($"Branch '{branch.Name}' is not active.");
        }

        return branch;
    }
}
