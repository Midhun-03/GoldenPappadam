using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Identity;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GoldenPappadam.Tests;

/// <summary>
/// The invoice as the office uses it, through real HTTP: finalize, get the stored PDF, print it,
/// email it, watch the email fail and retry it - and the concurrency guarantee with an admin PC
/// and a phone finalizing at the same moment.
/// </summary>
public class InvoiceDocumentApiTests : IAsyncLifetime
{
    private ApiFactory _api = null!;
    private HttpClient _office = null!;
    private HttpClient _phone = null!;
    private Guid _customerId;
    private Guid _productId;
    private Guid _deviceId;

    public async Task InitializeAsync()
    {
        _api = new ApiFactory();
        await _api.InitializeAsync();

        await _api.CreateUserAsync("office@test.local", Roles.Admin);
        await _api.CreateUserAsync("van@test.local", Roles.Salesperson);
        await SeedAsync();

        _office = await _api.SignInAsync("office@test.local");
        _phone = await _api.SignInAsync("van@test.local");

        var registered = await _phone.PostAsJsonAsync("/api/mobile/devices/register", new { name = "Nokia", platform = "android" });
        registered.EnsureSuccessStatusCode();
        _deviceId = (await registered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await WithDbAsync(async db =>
        {
            var device = await db.Devices.SingleAsync(d => d.Id == _deviceId);
            device.LocationId = KnownStockLocations.FirstVanId;
            await db.SaveChangesAsync();
        });
    }

    public async Task DisposeAsync()
    {
        _office.Dispose();
        _phone.Dispose();
        await _api.DisposeAsync();
    }

    [Fact]
    public async Task A_finalized_invoice_has_its_pdf_stored_and_serves_the_same_file_every_time()
    {
        var invoice = await CreateInvoiceAsync();

        Assert.Equal("GP/26-27/000001", invoice.GetProperty("invoiceNumber").GetString());
        Assert.Equal(JsonValueKind.Object, invoice.GetProperty("document").ValueKind);

        var id = invoice.GetProperty("id").GetGuid();
        var first = await _office.GetAsync($"/api/sales/invoices/{id}/pdf");
        var second = await _office.GetAsync($"/api/sales/invoices/{id}/pdf");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("application/pdf", first.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", first.Content.Headers.ContentDisposition?.DispositionType);

        var bytes = await first.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal(bytes, await second.Content.ReadAsByteArrayAsync());

        var download = await _office.GetAsync($"/api/sales/invoices/{id}/pdf?download=true");
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("Invoice-GP-26-27-000001.pdf", download.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
    }

    [Fact]
    public async Task A_sale_from_a_phone_gets_its_pdf_the_first_time_the_office_opens_it()
    {
        var saleId = await SubmitPhoneSaleAsync();

        var detail = await _office.GetFromJsonAsync<JsonElement>($"/api/sales/invoices/{saleId}");
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("document").ValueKind);
        Assert.Equal("Nokia", detail.GetProperty("recordedOnDevice").GetString());

        var pdf = await _office.GetAsync($"/api/sales/invoices/{saleId}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);

        detail = await _office.GetFromJsonAsync<JsonElement>($"/api/sales/invoices/{saleId}");
        Assert.Equal(JsonValueKind.Object, detail.GetProperty("document").ValueKind);
    }

    [Fact]
    public async Task A_failed_email_leaves_the_invoice_intact_and_can_be_retried()
    {
        var invoice = await CreateInvoiceAsync();
        var id = invoice.GetProperty("id").GetGuid();

        _api.Email.FailWith = "SMTP connection timeout";
        var failed = await EmailAsync(id);

        Assert.Equal("Failed", failed.GetProperty("status").GetString());
        Assert.Equal("SMTP connection timeout", failed.GetProperty("errorMessage").GetString());
        Assert.Equal("shop@example.com", failed.GetProperty("recipient").GetString());

        // Still finalized, still with its PDF, and findable as needing a resend.
        var detail = await _office.GetFromJsonAsync<JsonElement>($"/api/sales/invoices/{id}");
        Assert.Equal("Issued", detail.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Object, detail.GetProperty("document").ValueKind);

        var needingResend = await _office.GetFromJsonAsync<JsonElement>("/api/sales/invoices?emailStatus=Failed");
        Assert.Equal(1, needingResend.GetArrayLength());

        _api.Email.FailWith = null;
        var retried = await EmailAsync(id);

        Assert.Equal("Sent", retried.GetProperty("status").GetString());
        Assert.Equal(2, retried.GetProperty("attemptNumber").GetInt32());

        var message = Assert.Single(_api.Email.Sent);
        Assert.Equal("shop@example.com", message.To);
        Assert.Equal("Invoice GP/26-27/000001 — Golden Pappadam", message.Subject);
        var attachment = Assert.Single(message.Attachments);
        Assert.Equal("Invoice-GP-26-27-000001.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);

        var history = await _office.GetFromJsonAsync<JsonElement>($"/api/sales/invoices/{id}/emails");
        Assert.Equal(["Sent", "Failed"], history.EnumerateArray().Select(h => h.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task An_email_needs_an_address_and_is_never_sent_for_a_cancelled_invoice()
    {
        await WithDbAsync(async db =>
        {
            var customer = await db.Customers.SingleAsync();
            customer.Email = null;
            await db.SaveChangesAsync();
        });

        var invoice = await CreateInvoiceAsync();
        var id = invoice.GetProperty("id").GetGuid();

        var noAddress = await _office.PostAsJsonAsync($"/api/sales/invoices/{id}/email", new { recipient = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, noAddress.StatusCode);

        var typed = await EmailAsync(id, "accounts@example.com");
        Assert.Equal("Sent", typed.GetProperty("status").GetString());

        (await _office.PostAsJsonAsync($"/api/sales/invoices/{id}/cancel", new { reason = "Refused" })).EnsureSuccessStatusCode();

        var cancelled = await _office.PostAsJsonAsync($"/api/sales/invoices/{id}/email", new { recipient = "accounts@example.com" });
        Assert.Equal(HttpStatusCode.BadRequest, cancelled.StatusCode);
    }

    [Fact]
    public async Task Invoice_pdfs_email_and_settings_are_closed_to_the_sales_app_and_to_strangers()
    {
        var id = (await CreateInvoiceAsync()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await _phone.GetAsync($"/api/sales/invoices/{id}/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _phone.PostAsJsonAsync($"/api/sales/invoices/{id}/email", new { recipient = "x@example.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _phone.PostAsJsonAsync("/api/sales/invoices/preview", new { customerId = _customerId, lines = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _phone.GetAsync("/api/sales/invoice-settings")).StatusCode);

        using var stranger = _api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync($"/api/sales/invoices/{id}/pdf")).StatusCode);
    }

    [Fact]
    public async Task The_office_can_switch_gst_on_but_not_with_a_gstin_from_another_state()
    {
        var settings = await _office.GetFromJsonAsync<JsonElement>("/api/sales/invoice-settings");
        Assert.False(settings.GetProperty("gstEnabled").GetBoolean());
        Assert.Equal("32", settings.GetProperty("stateCode").GetString());

        var tamilGstin = await _office.PutAsJsonAsync("/api/sales/invoice-settings", Settings("33AAAAA1234A1Z5", "GP"));
        Assert.Equal(HttpStatusCode.BadRequest, tamilGstin.StatusCode);

        var longPrefix = await _office.PutAsJsonAsync("/api/sales/invoice-settings", Settings(null, "GPSX"));
        Assert.Equal(HttpStatusCode.BadRequest, longPrefix.StatusCode);

        var saved = await _office.PutAsJsonAsync("/api/sales/invoice-settings", Settings("32aaaaa1234a1z5", "GP"));
        saved.EnsureSuccessStatusCode();
        var body = await saved.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("gstEnabled").GetBoolean());
        Assert.Equal("32AAAAA1234A1Z5", body.GetProperty("gstin").GetString());
        Assert.StartsWith("GP/", body.GetProperty("nextInvoiceNumber").GetString());
    }

    [Fact]
    public async Task Two_office_computers_and_a_phone_finalizing_together_never_share_a_number()
    {
        var secondOffice = await _api.SignInAsync("office@test.local");

        var office1 = Enumerable.Range(0, 8).Select(_ => CreateInvoiceAsync(_office));
        var office2 = Enumerable.Range(0, 8).Select(_ => CreateInvoiceAsync(secondOffice));
        var phone = Enumerable.Range(0, 4).Select(_ => SubmitPhoneSaleAsync());

        await Task.WhenAll(
            Task.WhenAll(office1),
            Task.WhenAll(office2),
            Task.WhenAll(phone));

        await WithDbAsync(async db =>
        {
            var numbers = await db.Invoices.OrderBy(i => i.SequenceNumber).Select(i => i.SequenceNumber).ToListAsync();

            Assert.Equal(Enumerable.Range(1, 20), numbers);
            Assert.Equal(20, await db.Invoices.Select(i => i.InvoiceNumber).Distinct().CountAsync());
            Assert.Equal(20, (await db.InvoiceNumberSequences.SingleAsync()).LastNumber);
        });

        secondOffice.Dispose();
    }

    private Task<JsonElement> CreateInvoiceAsync() => CreateInvoiceAsync(_office);

    private async Task<JsonElement> CreateInvoiceAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/sales/invoices", new
        {
            customerId = _customerId,
            invoiceDate = "2026-09-23",
            discountAmount = 0,
            lines = new[] { new { productId = _productId, quantity = 5 } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("invoice");
    }

    /// <summary>A sale exactly as the Flutter app sends one; returns the invoice the server made.</summary>
    private async Task<Guid> SubmitPhoneSaleAsync()
    {
        var response = await _phone.PostAsJsonAsync("/api/mobile/sync/submissions", new
        {
            deviceId = _deviceId,
            items = new[]
            {
                new
                {
                    clientRequestId = Guid.NewGuid(),
                    type = "Invoice",
                    recordedAt = "2026-09-23T06:30:00.000Z",
                    sale = new
                    {
                        customerId = _customerId,
                        lines = new[] { new { productId = _productId, quantity = 2.0, unitPrice = 35.0 } },
                        pricesAsOf = "2026-09-23T05:00:00.000Z",
                        notes = (string?)null
                    }
                }
            }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("results")[0];
        Assert.Equal("Accepted", result.GetProperty("outcome").GetString());

        return result.GetProperty("recordId").GetGuid();
    }

    private async Task<JsonElement> EmailAsync(Guid id, string? recipient = null)
    {
        var response = await _office.PostAsJsonAsync($"/api/sales/invoices/{id}/email", new { recipient });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static object Settings(string? gstin, string series) => new
    {
        legalName = "Golden Pappadam",
        address = "Kundara, Kollam, Kerala",
        phone = "0474 000000",
        email = "billing@example.com",
        gstin,
        stateCode = "32",
        seriesCode = series,
        pricesIncludeTax = true,
        roundToNearestRupee = false,
        paymentTerms = "Payable on next delivery",
        bankDetails = (string?)null,
        termsAndConditions = (string?)null
    };

    private async Task SeedAsync() => await WithDbAsync(async db =>
    {
        var category = new ProductCategory { Name = "Pappadam" };
        db.Add(category);
        await db.SaveChangesAsync();

        var packet = await db.UnitOfMeasures.FirstAsync(u => u.Code == "PKT");
        var product = new Product
        {
            ProductCode = "PKT-20",
            Name = "Pappadam 200 g",
            CategoryId = category.Id,
            Kind = ProductKind.Loose,
            UnitOfMeasureId = packet.Id,
            SellingPrice = 45.50m
        };
        db.Add(product);

        var customer = new Customer
        {
            Name = "Danya Supermarket",
            Address = "Kundara",
            Email = "shop@example.com",
            StateCode = "32"
        };
        db.Add(customer);
        await db.SaveChangesAsync();

        db.Add(new CustomerPrice { CustomerId = customer.Id, ProductId = product.Id, UnitPrice = 35m });
        await db.SaveChangesAsync();

        _customerId = customer.Id;
        _productId = product.Id;
    });

    private async Task WithDbAsync(Func<AppDbContext, Task> work)
    {
        using var scope = _api.Services.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
