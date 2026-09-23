using GoldenPappadam.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace GoldenPappadam.Api.Features.Sales.Settings;

/// <summary>Admin-only, through the fallback policy, like every office endpoint.</summary>
[ApiController]
[Route("api/sales/invoice-settings")]
public class InvoiceSettingsController(InvoiceSettingsService settings) : ControllerBase
{
    [HttpGet]
    public Task<InvoiceSettingsDto> Get(CancellationToken ct) => settings.GetAsync(ct);

    [HttpPut]
    public Task<InvoiceSettingsDto> Save(SaveInvoiceSettingsRequest request, CancellationToken ct) =>
        settings.SaveAsync(request, ct);

    /// <summary>States with their GST codes, for every state picker in the panel.</summary>
    [HttpGet("/api/sales/states")]
    public IEnumerable<StateDto> States() => IndianStates.All.Select(s => new StateDto(s.Code, s.Name));
}
