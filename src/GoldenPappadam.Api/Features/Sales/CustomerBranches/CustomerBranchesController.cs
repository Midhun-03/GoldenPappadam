using Microsoft.AspNetCore.Mvc;

namespace GoldenPappadam.Api.Features.Sales.CustomerBranches;

/// <summary>
/// The physical shops under a multi-branch customer, e.g. "Danya Supermarket - Kundara" under
/// "Danya Supermarket". Admin-only through the fallback policy, same as customers and prices.
/// </summary>
[ApiController]
[Route("api/sales/customers/{customerId:guid}/branches")]
public class CustomerBranchesController(CustomerBranchService branches) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CustomerBranchDto>> GetAll(
        Guid customerId,
        bool includeInactive = false,
        CancellationToken ct = default) =>
        branches.GetForCustomerAsync(customerId, includeInactive, ct);

    [HttpPost]
    public async Task<ActionResult<CustomerBranchDto>> Create(
        Guid customerId,
        SaveCustomerBranchRequest request,
        CancellationToken ct)
    {
        var branch = await branches.CreateAsync(customerId, request, ct);

        return CreatedAtAction(nameof(GetAll), new { customerId }, branch);
    }

    [HttpPut("{branchId:guid}")]
    public Task<CustomerBranchDto> Update(
        Guid customerId,
        Guid branchId,
        SaveCustomerBranchRequest request,
        CancellationToken ct) =>
        branches.UpdateAsync(customerId, branchId, request, ct);

    [HttpPost("{branchId:guid}/active")]
    public Task<CustomerBranchDto> SetActive(
        Guid customerId,
        Guid branchId,
        bool isActive,
        CancellationToken ct) =>
        branches.SetActiveAsync(customerId, branchId, isActive, ct);
}
