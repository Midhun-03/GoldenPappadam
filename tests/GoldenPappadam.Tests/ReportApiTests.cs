using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Identity;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GoldenPappadam.Tests;

/// <summary>
/// The three outputs of a report through real HTTP: the screen's JSON, a PDF and an Excel sheet,
/// all carrying the same totals - and none of it reachable by the sales app.
/// </summary>
public class ReportApiTests : IAsyncLifetime
{
    private ApiFactory _api = null!;
    private HttpClient _office = null!;
    private Guid _customerId;

    public async Task InitializeAsync()
    {
        _api = new ApiFactory();
        await _api.InitializeAsync();
        await _api.CreateUserAsync("office@test.local", Roles.Admin);
        await _api.CreateUserAsync("van@test.local", Roles.Salesperson);
        _office = await _api.SignInAsync("office@test.local");

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var category = new ProductCategory { Name = "Pappadam" };
        db.Add(category);
        await db.SaveChangesAsync();

        var product = new Product
        {
            ProductCode = "LOOSE",
            Name = "Pappadam 200 g",
            CategoryId = category.Id,
            Kind = ProductKind.Loose,
            UnitOfMeasureId = (await db.UnitOfMeasures.FirstAsync(u => u.Code == "PKT")).Id,
            SellingPrice = 37m
        };
        var customer = new Customer { Name = "Danya Supermarket" };
        db.AddRange(product, customer);
        await db.SaveChangesAsync();
        _customerId = customer.Id;

        (await _office.PostAsJsonAsync("/api/sales/invoices", new
        {
            customerId = customer.Id,
            invoiceDate = "2026-09-23",
            discountAmount = 0,
            lines = new[] { new { productId = product.Id, quantity = 40 } }
        })).EnsureSuccessStatusCode();
    }

    public async Task DisposeAsync()
    {
        _office.Dispose();
        await _api.DisposeAsync();
    }

    [Fact]
    public async Task The_screen_the_pdf_and_the_excel_sheet_carry_the_same_total()
    {
        var json = await _office.GetFromJsonAsync<JsonElement>("/api/reports/sales?from=2026-09-23&to=2026-09-23");
        var sales = json.GetProperty("summary").EnumerateArray()
            .Single(f => f.GetProperty("label").GetString() == "Sales").GetProperty("value").GetDecimal();
        Assert.Equal(1_480m, sales);   // 40 × 37

        var pdf = await _office.GetAsync("/api/reports/sales?from=2026-09-23&format=pdf");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.Equal("sales-2026-09-23.pdf", pdf.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString((await pdf.Content.ReadAsByteArrayAsync())[..4]));

        var xlsx = await _office.GetAsync("/api/reports/sales?from=2026-09-23&format=xlsx");
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);

        using var workbook = new XLWorkbook(await xlsx.Content.ReadAsStreamAsync());
        var sheet = workbook.Worksheets.Single();
        var totalRow = sheet.RowsUsed().First(r => r.Cell(1).GetString() == "Total");

        // A real number, not text that looks like one, so the accountant can add it up.
        Assert.Contains(totalRow.CellsUsed(), c => c.DataType == XLDataType.Number && c.GetValue<decimal>() == 1_480m);
    }

    [Fact]
    public async Task Every_report_answers_for_the_office()
    {
        foreach (var path in new[]
                 {
                     "/api/reports/sales", "/api/reports/collections", "/api/reports/outstanding",
                     "/api/reports/stock", "/api/reports/stock-age", "/api/inventory/stock/age",
                     "/api/inventory/repacking",
                     $"/api/reports/statement/{_customerId}?from=2026-09-01&to=2026-09-30",
                     "/api/reports/returns?from=2026-09-01&to=2026-09-30&format=pdf",
                     "/api/reports/returns?format=xlsx",
                     "/api/sales/returns"
                 })
        {
            var response = await _office.GetAsync(path);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path} answered {response.StatusCode}");
        }
    }

    [Fact]
    public async Task Reports_are_closed_to_the_sales_app_and_to_strangers()
    {
        using var phone = await _api.SignInAsync("van@test.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await phone.GetAsync("/api/reports/sales")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await phone.GetAsync("/api/reports/outstanding?format=pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await phone.GetAsync("/api/inventory/stock/age")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await phone.PostAsJsonAsync("/api/inventory/repacking", new { fromProductId = Guid.NewGuid(), fromQuantity = 1, toProductId = Guid.NewGuid() })).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await phone.GetAsync("/api/reports/returns")).StatusCode);

        // Settling a return - above all granting a credit - is the office's decision.
        Assert.Equal(HttpStatusCode.Forbidden, (await phone.GetAsync("/api/sales/returns")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await phone.PostAsJsonAsync("/api/sales/returns", new { customerId = _customerId, lines = new[] { new { productId = Guid.NewGuid(), quantity = 1, reason = "Expired" } }, settlement = "Credit" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await phone.PostAsJsonAsync($"/api/sales/returns/{Guid.NewGuid()}/settle", new { settlement = "Credit" })).StatusCode);

        using var stranger = _api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/api/reports/collections")).StatusCode);
    }

    [Fact]
    public async Task A_backwards_period_is_explained_not_crashed()
    {
        var response = await _office.GetAsync("/api/reports/sales?from=2026-09-23&to=2026-09-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("start date", await response.Content.ReadAsStringAsync());
    }
}
