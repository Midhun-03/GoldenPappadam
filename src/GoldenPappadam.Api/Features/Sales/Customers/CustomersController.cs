using GoldenPappadam.Api.Common;
using GoldenPappadam.Infrastructure.Identity;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Sales.Customers;

[ApiController]
[Route("api/sales/customers")]
public class CustomersController(AppDbContext db, CustomerService customers) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<CustomerDto>> GetAll(
        string? search = null,
        bool includeInactive = false,
        bool withBalanceOnly = false,
        bool addedBySales = false,
        CancellationToken ct = default)
    {
        var rows = await CustomerQueries.Project(
                db.Customers
                    .Where(c => includeInactive || c.IsActive)
                    .Where(c => search == null || c.Name.Contains(search) || c.Phone!.Contains(search))
                    .Where(c => !addedBySales || db.UserRoles.Any(ur =>
                        ur.UserId == c.CreatedBy &&
                        db.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Salesperson)))
                    .OrderBy(c => c.Name),
                db)
            .ToListAsync(ct);

        return withBalanceOnly ? rows.Where(c => c.Balance > 0m).ToList() : rows;
    }

    [HttpGet("{id:guid}")]
    public async Task<CustomerDto> GetById(Guid id, CancellationToken ct) =>
        await CustomerQueries.Project(db.Customers.Where(c => c.Id == id), db).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Customer");

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create(SaveCustomerRequest request, CancellationToken ct)
    {
        var customer = await customers.CreateAsync(request, ct);
        var dto = await GetById(customer.Id, ct);

        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<CustomerDto> Update(Guid id, SaveCustomerRequest request, CancellationToken ct)
    {
        await customers.UpdateAsync(id, request, ct);

        return await GetById(id, ct);
    }

    /// <summary>Customers are deactivated, never deleted, because their bills refer to them.</summary>
    [HttpPost("{id:guid}/active")]
    public async Task<CustomerDto> SetActive(Guid id, bool isActive, CancellationToken ct)
    {
        await customers.SetActiveAsync(id, isActive, ct);

        return await GetById(id, ct);
    }

    /// <summary>Account statement: opening balance, bills and payments with a running balance.</summary>
    [HttpGet("{id:guid}/ledger")]
    public Task<IReadOnlyList<LedgerEntryDto>> GetLedger(Guid id, CancellationToken ct) =>
        customers.GetLedgerAsync(id, ct);

    [HttpGet("{id:guid}/outstanding-invoices")]
    public Task<IReadOnlyList<OutstandingInvoiceDto>> GetOutstandingInvoices(Guid id, CancellationToken ct) =>
        customers.GetOutstandingInvoicesAsync(id, ct);
}
