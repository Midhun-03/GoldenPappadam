using Microsoft.AspNetCore.Mvc;

namespace GoldenPappadam.Api.Features.Staff.Employees;

/// <summary>Admin-only, like every endpoint without an attribute (the fallback policy).</summary>
[ApiController]
[Route("api/staff/employees")]
public class EmployeesController(EmployeeService employees) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<EmployeeDto>> GetAll(string? search = null, bool includeInactive = false, CancellationToken ct = default) =>
        employees.GetAllAsync(string.IsNullOrWhiteSpace(search) ? null : search.Trim(), includeInactive, ct);

    [HttpGet("{id:guid}")]
    public Task<EmployeeDto> GetById(Guid id, CancellationToken ct) => employees.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<EmployeeDto>> Create(SaveEmployeeRequest request, CancellationToken ct)
    {
        var employee = await employees.CreateAsync(request, ct);

        return CreatedAtAction(nameof(GetById), new { id = employee.Id }, employee);
    }

    [HttpPut("{id:guid}")]
    public Task<EmployeeDto> Update(Guid id, SaveEmployeeRequest request, CancellationToken ct) =>
        employees.UpdateAsync(id, request, ct);

    /// <summary>Employees are deactivated, never deleted, because their attendance and payments refer to them.</summary>
    [HttpPost("{id:guid}/active")]
    public Task<EmployeeDto> SetActive(Guid id, bool isActive, CancellationToken ct) =>
        employees.SetActiveAsync(id, isActive, ct);

    [HttpGet("{id:guid}/wage-rates")]
    public Task<IReadOnlyList<WageRateDto>> GetWageRates(Guid id, CancellationToken ct) =>
        employees.GetWageRatesAsync(id, ct);

    /// <summary>Changes the wage from a date by adding to the history - the old rate is never overwritten.</summary>
    [HttpPost("{id:guid}/wage-rates")]
    public Task<IReadOnlyList<WageRateDto>> AddWageRate(Guid id, AddWageRateRequest request, CancellationToken ct) =>
        employees.AddWageRateAsync(id, request, ct);
}
