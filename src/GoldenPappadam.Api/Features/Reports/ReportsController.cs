using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Reports;

public enum ReportFormat
{
    Json,
    Pdf,
    Xlsx
}

/// <summary>
/// Admin-only through the fallback policy. Each report is one document shown three ways: JSON for
/// the screen, a PDF to print and an Excel sheet to work with - the same figures in all three.
/// Dates are IST business days; leaving them out means today.
/// </summary>
[ApiController]
[Route("api/reports")]
public class ReportsController(
    AppDbContext db,
    SalesReport sales,
    CollectionsReport collections,
    OutstandingReport outstanding,
    StatementReport statement,
    StockMovementReport stock,
    StockAgeReport stockAge,
    ReturnsReport returns) : ControllerBase
{
    [HttpGet("sales")]
    public async Task<IActionResult> Sales(DateOnly? from, DateOnly? to, ReportFormat format = ReportFormat.Json, CancellationToken ct = default)
    {
        var (start, end) = ReportPeriod.Resolve(from, to);
        return await RespondAsync(await sales.BuildAsync(start, end, ct), format, ct);
    }

    [HttpGet("collections")]
    public async Task<IActionResult> Collections(DateOnly? from, DateOnly? to, ReportFormat format = ReportFormat.Json, CancellationToken ct = default)
    {
        var (start, end) = ReportPeriod.Resolve(from, to);
        return await RespondAsync(await collections.BuildAsync(start, end, ct), format, ct);
    }

    [HttpGet("outstanding")]
    public async Task<IActionResult> Outstanding(DateOnly? asOf, ReportFormat format = ReportFormat.Json, CancellationToken ct = default) =>
        await RespondAsync(await outstanding.BuildAsync(asOf ?? IndiaTime.Today(), ct), format, ct);

    [HttpGet("stock")]
    public async Task<IActionResult> Stock(DateOnly? from, DateOnly? to, ReportFormat format = ReportFormat.Json, CancellationToken ct = default)
    {
        var (start, end) = ReportPeriod.Resolve(from, to);
        return await RespondAsync(await stock.BuildAsync(start, end, ct), format, ct);
    }

    [HttpGet("stock-age")]
    public async Task<IActionResult> StockAge(DateOnly? asOf, ReportFormat format = ReportFormat.Json, CancellationToken ct = default) =>
        await RespondAsync(await stockAge.BuildAsync(asOf ?? IndiaTime.Today(), ct), format, ct);

    [HttpGet("returns")]
    public async Task<IActionResult> Returns(DateOnly? from, DateOnly? to, ReportFormat format = ReportFormat.Json, CancellationToken ct = default)
    {
        var (start, end) = ReportPeriod.Resolve(from, to);
        return await RespondAsync(await returns.BuildAsync(start, end, ct), format, ct);
    }

    [HttpGet("statement/{customerId:guid}")]
    public async Task<IActionResult> Statement(
        Guid customerId,
        DateOnly? from,
        DateOnly? to,
        ReportFormat format = ReportFormat.Json,
        CancellationToken ct = default)
    {
        var (start, end) = ReportPeriod.Resolve(from, to);
        return await RespondAsync(await statement.BuildAsync(customerId, start, end, ct), format, ct);
    }

    private async Task<IActionResult> RespondAsync(ReportDocument report, ReportFormat format, CancellationToken ct)
    {
        if (format == ReportFormat.Json)
        {
            return Ok(report);
        }

        var settings = await db.InvoiceSettings.AsNoTracking().SingleAsync(s => s.Id == InvoiceSettings.SingletonId, ct);
        var name = report.From == report.To
            ? $"{report.Name}-{report.From:yyyy-MM-dd}"
            : $"{report.Name}-{report.From:yyyy-MM-dd}-to-{report.To:yyyy-MM-dd}";

        Response.Headers.CacheControl = "private, no-store";

        return format == ReportFormat.Pdf
            ? File(ReportPdfRenderer.Render(report, new ReportPdfRenderer.Business(settings.LegalName, settings.Address)),
                "application/pdf", $"{name}.pdf")
            : File(ReportExcelWriter.Write(report, settings.LegalName),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{name}.xlsx");
    }
}
