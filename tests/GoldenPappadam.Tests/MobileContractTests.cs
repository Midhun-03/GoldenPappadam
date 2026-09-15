using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Identity;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GoldenPappadam.Tests;

/// <summary>
/// The join between the two halves of phase 3.
///
/// The Dart tests prove the phone builds the right JSON and the C# tests prove the server does the
/// right thing - but neither notices if the two disagree about a field name. These send the exact
/// body the Flutter app produces, written out by hand so that a rename on either side fails here
/// rather than on a road in Kollam.
///
/// The shapes come from mobile/lib/data/sales_repository.dart and the envelope from
/// mobile/lib/sync/sync_engine.dart.
/// </summary>
public class MobileContractTests : IAsyncLifetime
{
    private ApiFactory _api = null!;
    private HttpClient _phone = null!;
    private Guid _deviceId;
    private Guid _customerId;
    private Guid _productId;

    public async Task InitializeAsync()
    {
        _api = new ApiFactory();
        await _api.InitializeAsync();

        await _api.CreateUserAsync("van@test.local", Roles.Salesperson);
        await SeedShopAndProductAsync();

        _phone = await _api.SignInAsync("van@test.local");

        var registered = await _phone.PostAsJsonAsync(
            "/api/mobile/devices/register", new { name = "Nokia", platform = "android" });

        registered.EnsureSuccessStatusCode();
        var device = await registered.Content.ReadFromJsonAsync<JsonElement>();
        _deviceId = device.GetProperty("id").GetGuid();
    }

    public async Task DisposeAsync()
    {
        _phone.Dispose();
        await _api.DisposeAsync();
    }

    [Fact]
    public async Task The_body_the_app_sends_for_a_credit_sale_is_accepted()
    {
        var saleId = Guid.NewGuid();
        var visitId = Guid.NewGuid();

        var response = await SubmitAsync($$"""
            {
              "deviceId": "{{_deviceId}}",
              "items": [
                {
                  "clientRequestId": "{{saleId}}",
                  "type": "Invoice",
                  "recordedAt": "2026-09-15T06:30:00.000Z",
                  "sale": {
                    "customerId": "{{_customerId}}",
                    "lines": [
                      { "productId": "{{_productId}}", "quantity": 10.0, "unitPrice": 35.0 }
                    ],
                    "pricesAsOf": "2026-09-15T05:00:00.000Z",
                    "notes": null
                  }
                },
                {
                  "clientRequestId": "{{visitId}}",
                  "type": "Visit",
                  "recordedAt": "2026-09-15T06:30:00.000Z",
                  "visit": {
                    "customerId": "{{_customerId}}",
                    "outcome": "Sold",
                    "saleClientRequestId": "{{saleId}}",
                    "paymentClientRequestId": null,
                    "notes": null
                  }
                }
              ]
            }
            """);

        var results = response.GetProperty("results");

        Assert.Equal("Accepted", results[0].GetProperty("outcome").GetString());
        Assert.Equal("Accepted", results[1].GetProperty("outcome").GetString());

        // The visit found the bill by the id the phone gave it, before either had a server id.
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var visit = await db.ShopVisits.SingleAsync();

        Assert.NotNull(visit.InvoiceId);
        Assert.Equal(results[0].GetProperty("recordId").GetGuid(), visit.InvoiceId);
    }

    [Fact]
    public async Task The_body_the_app_sends_for_a_sale_paid_on_the_spot_is_accepted()
    {
        var saleId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var response = await SubmitAsync($$"""
            {
              "deviceId": "{{_deviceId}}",
              "items": [
                {
                  "clientRequestId": "{{saleId}}",
                  "type": "Invoice",
                  "recordedAt": "2026-09-15T07:00:00.000Z",
                  "sale": {
                    "customerId": "{{_customerId}}",
                    "lines": [
                      { "productId": "{{_productId}}", "quantity": 10.0, "unitPrice": 35.0 }
                    ],
                    "pricesAsOf": "2026-09-15T05:00:00.000Z",
                    "notes": null
                  }
                },
                {
                  "clientRequestId": "{{paymentId}}",
                  "type": "Payment",
                  "recordedAt": "2026-09-15T07:00:00.000Z",
                  "payment": {
                    "customerId": "{{_customerId}}",
                    "amount": 350.0,
                    "method": "UPI",
                    "reference": null,
                    "notes": null
                  }
                }
              ]
            }
            """);

        var results = response.GetProperty("results");

        Assert.Equal("Accepted", results[0].GetProperty("outcome").GetString());
        Assert.Equal("Accepted", results[1].GetProperty("outcome").GetString());

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Billed 350 and paid 350, so the shop owes nothing more for it.
        Assert.Equal(350m, await db.Invoices.SumAsync(i => i.TotalAmount));
        Assert.Equal(350m, await db.Payments.SumAsync(p => p.Amount));
    }

    [Fact]
    public async Task The_body_the_app_sends_for_a_visit_with_no_order_is_accepted()
    {
        var response = await SubmitAsync($$"""
            {
              "deviceId": "{{_deviceId}}",
              "items": [
                {
                  "clientRequestId": "{{Guid.NewGuid()}}",
                  "type": "Visit",
                  "recordedAt": "2026-09-15T08:00:00.000Z",
                  "visit": {
                    "customerId": "{{_customerId}}",
                    "outcome": "NoOrder",
                    "saleClientRequestId": null,
                    "paymentClientRequestId": null,
                    "notes": "Still had stock"
                  }
                }
              ]
            }
            """);

        Assert.Equal("Accepted", response.GetProperty("results")[0].GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task The_snapshot_has_every_field_the_app_reads_out_of_it()
    {
        var snapshot = await _phone.GetFromJsonAsync<JsonElement>("/api/mobile/sync/snapshot");

        // Each of these is read by name in mobile/lib/sync/sync_engine.dart.
        Assert.True(snapshot.TryGetProperty("serverTime", out _));
        Assert.True(snapshot.TryGetProperty("pricesAsOf", out _));
        Assert.True(snapshot.TryGetProperty("vanLocationId", out _));
        Assert.True(snapshot.TryGetProperty("paymentMethods", out _));

        var customer = snapshot.GetProperty("customers")[0];
        foreach (var field in new[] { "id", "name", "contactPerson", "phone", "address", "balance" })
        {
            Assert.True(customer.TryGetProperty(field, out _), $"customer.{field} is missing");
        }

        var product = snapshot.GetProperty("products")[0];
        foreach (var field in new[] { "id", "productCode", "name", "unitCode", "defaultPrice" })
        {
            Assert.True(product.TryGetProperty(field, out _), $"product.{field} is missing");
        }
    }

    [Fact]
    public async Task A_submission_result_has_every_field_the_app_reads_out_of_it()
    {
        var response = await SubmitAsync($$"""
            {
              "deviceId": "{{_deviceId}}",
              "items": [
                {
                  "clientRequestId": "{{Guid.NewGuid()}}",
                  "type": "Visit",
                  "recordedAt": "2026-09-15T08:00:00.000Z",
                  "visit": {
                    "customerId": "{{_customerId}}",
                    "outcome": "Skipped",
                    "saleClientRequestId": null,
                    "paymentClientRequestId": null,
                    "notes": null
                  }
                }
              ]
            }
            """);

        var result = response.GetProperty("results")[0];

        foreach (var field in new[] { "clientRequestId", "outcome", "recordId", "error", "priceMismatch" })
        {
            Assert.True(result.TryGetProperty(field, out _), $"result.{field} is missing");
        }
    }

    [Fact]
    public async Task The_payment_methods_the_app_offers_are_all_real()
    {
        // The sale sheet offers Cash and UPI; the payment screen adds Cheque. If any of these ever
        // stops being a PaymentMethod on the server, a salesperson finds out in a shop.
        var snapshot = await _phone.GetFromJsonAsync<JsonElement>("/api/mobile/sync/snapshot");
        var methods = snapshot.GetProperty("paymentMethods").EnumerateArray()
            .Select(m => m.GetString())
            .ToList();

        Assert.Contains("Cash", methods);
        Assert.Contains("UPI", methods);
        Assert.Contains("Cheque", methods);
    }

    private async Task<JsonElement> SubmitAsync(string json)
    {
        var response = await _phone.PostAsync(
            "/api/mobile/sync/submissions",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task SeedShopAndProductAsync()
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var category = new ProductCategory { Name = "Pappadam" };
        db.Add(category);
        await db.SaveChangesAsync();

        var packet = await db.UnitOfMeasures.FirstAsync(u => u.Code == "PKT");

        var product = new Product
        {
            ProductCode = "PKT-20",
            Name = "20 piece packet",
            CategoryId = category.Id,
            Kind = ProductKind.Packed,
            UnitOfMeasureId = packet.Id,
            SourceProductId = null,
            SourceQuantityPerPack = null,
            SellingPrice = 45m
        };

        // A loose product to be packed from, so the check constraint is satisfied.
        var loose = new Product
        {
            ProductCode = "LOOSE",
            Name = "Loose pappadam",
            CategoryId = category.Id,
            Kind = ProductKind.Loose,
            UnitOfMeasureId = (await db.UnitOfMeasures.FirstAsync(u => u.Code == "KG")).Id
        };

        db.Add(loose);
        await db.SaveChangesAsync();

        product.SourceProductId = loose.Id;
        product.SourceQuantityPerPack = 20m;
        db.Add(product);

        var customer = new Customer { Name = "Kumar Stores", Phone = "9847012345" };
        db.Add(customer);
        await db.SaveChangesAsync();

        db.Add(new CustomerPrice { CustomerId = customer.Id, ProductId = product.Id, UnitPrice = 35m });
        db.Add(new StockMovement
        {
            ProductId = product.Id,
            LocationId = KnownStockLocations.FirstVanId,
            MovementType = StockMovementType.Opening,
            Quantity = 150m,
            OccurredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        _customerId = customer.Id;
        _productId = product.Id;
    }
}
