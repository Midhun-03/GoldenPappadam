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

        await AssignVanAsync();
    }

    /// <summary>What the office does once: this phone rides in that van.</summary>
    private async Task AssignVanAsync()
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var device = await db.Devices.SingleAsync(d => d.Id == _deviceId);
        device.LocationId = KnownStockLocations.FirstVanId;
        await db.SaveChangesAsync();
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
    public async Task The_body_the_app_sends_for_a_van_load_is_accepted()
    {
        // Exactly what SalesRepository.recordVanLoad writes: lines, and nothing that could name a
        // location or a direction.
        var response = await SubmitAsync($$"""
            {
              "deviceId": "{{_deviceId}}",
              "items": [
                {
                  "clientRequestId": "{{Guid.NewGuid()}}",
                  "type": "VanLoad",
                  "recordedAt": "2026-09-16T01:00:00.000Z",
                  "vanLoad": {
                    "lines": [
                      { "productId": "{{_productId}}", "quantity": 350.0 }
                    ],
                    "notes": null
                  }
                }
              ]
            }
            """);

        Assert.Equal("Accepted", response.GetProperty("results")[0].GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task The_body_the_app_sends_for_a_stock_request_is_accepted()
    {
        var response = await SubmitAsync($$"""
            {
              "deviceId": "{{_deviceId}}",
              "items": [
                {
                  "clientRequestId": "{{Guid.NewGuid()}}",
                  "type": "StockRequest",
                  "recordedAt": "2026-09-16T01:00:00.000Z",
                  "stockRequest": {
                    "requiredDate": "2026-09-17",
                    "lines": [
                      { "productId": "{{_productId}}", "quantity": 250.0 }
                    ],
                    "notes": "Pepper sells well on Fridays"
                  }
                }
              ]
            }
            """);

        Assert.Equal("Accepted", response.GetProperty("results")[0].GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task The_body_the_app_sends_for_packets_collected_and_replaced_from_the_van_is_accepted()
    {
        // Exactly what SalesRepository.recordReturn writes: what came back and whether fresh packets
        // went from the van - no rate, no credit and no location.
        var response = await SubmitAsync($$"""
            {
              "deviceId": "{{_deviceId}}",
              "items": [
                {
                  "clientRequestId": "{{Guid.NewGuid()}}",
                  "type": "Return",
                  "recordedAt": "2026-09-26T05:00:00.000Z",
                  "return": {
                    "customerId": "{{_customerId}}",
                    "lines": [
                      { "productId": "{{_productId}}", "quantity": 4.0, "reason": "Expired" },
                      { "productId": "{{_productId}}", "quantity": 1.0, "reason": "Damaged" }
                    ],
                    "replacedFromVan": true,
                    "notes": "Behind the counter"
                  }
                }
              ]
            }
            """);

        var result = response.GetProperty("results")[0];
        Assert.Equal("Accepted", result.GetProperty("outcome").GetString());
        Assert.StartsWith("RN/", result.GetProperty("documentNumber").GetString());

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var note = await db.ReturnNotes.Include(r => r.Lines).SingleAsync();

        Assert.Equal(ReturnSettlement.Replacement, note.Settlement);
        Assert.Equal(175m, note.Value); // 5 packets at the shop's 35, not the phone's say-so
    }

    [Fact]
    public async Task The_body_the_app_sends_for_packets_left_for_the_office_is_accepted()
    {
        var response = await SubmitAsync($$"""
            {
              "deviceId": "{{_deviceId}}",
              "items": [
                {
                  "clientRequestId": "{{Guid.NewGuid()}}",
                  "type": "Return",
                  "recordedAt": "2026-09-26T05:00:00.000Z",
                  "return": {
                    "customerId": "{{_customerId}}",
                    "lines": [
                      { "productId": "{{_productId}}", "quantity": 3.0, "reason": "Expired" }
                    ],
                    "replacedFromVan": false,
                    "notes": null
                  }
                }
              ]
            }
            """);

        Assert.Equal("Accepted", response.GetProperty("results")[0].GetProperty("outcome").GetString());

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(ReturnSettlement.Pending, (await db.ReturnNotes.SingleAsync()).Settlement);
    }

    [Fact]
    public async Task The_office_returns_screens_are_closed_to_the_phone()
    {
        // The phone reaches returns only through its own sync endpoint, which cannot credit.
        Assert.Equal(HttpStatusCode.Forbidden, (await _phone.GetAsync("/api/sales/returns")).StatusCode);
    }

    [Fact]
    public async Task The_van_screen_has_every_field_the_app_reads_out_of_it()
    {
        var van = await _phone.GetFromJsonAsync<JsonElement>("/api/mobile/van-stock");

        Assert.True(van.TryGetProperty("lines", out var lines));
        Assert.True(van.TryGetProperty("isSettled", out _));

        // The van has opening stock from the seed, so there is a line to check the shape of.
        foreach (var field in new[] { "productName", "loaded", "sold", "returned", "replaced", "unaccounted" })
        {
            Assert.True(lines[0].TryGetProperty(field, out _), $"line.{field} is missing");
        }
    }

    [Fact]
    public async Task The_day_summary_has_every_field_the_home_screen_reads()
    {
        var day = await _phone.GetFromJsonAsync<JsonElement>("/api/mobile/day");

        foreach (var field in new[]
                 {
                     "totalSales", "saleCount", "shopsVisited", "cashCollected",
                     "creditSales", "noSaleVisits", "totalOutstanding"
                 })
        {
            Assert.True(day.TryGetProperty(field, out _), $"day.{field} is missing");
        }
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
        foreach (var field in new[]
                 { "id", "name", "contactPerson", "phone", "address", "balance", "hasMultipleBranches", "gstin", "isGstRegistered" })
        {
            Assert.True(customer.TryGetProperty(field, out _), $"customer.{field} is missing");
        }

        var product = snapshot.GetProperty("products")[0];
        foreach (var field in new[] { "id", "productCode", "name", "unitCode", "defaultPrice" })
        {
            Assert.True(product.TryGetProperty(field, out _), $"product.{field} is missing");
        }

        Assert.True(snapshot.TryGetProperty("payments", out _), "snapshot.payments is missing");
        Assert.True(snapshot.TryGetProperty("branches", out _), "snapshot.branches is missing");
    }

    [Fact]
    public async Task A_multi_branch_customers_branches_appear_in_the_snapshot_with_every_field_the_app_reads()
    {
        await MakeShopMultiBranchAsync();

        var snapshot = await _phone.GetFromJsonAsync<JsonElement>("/api/mobile/sync/snapshot");

        var customer = snapshot.GetProperty("customers").EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == _customerId);
        Assert.True(customer.GetProperty("hasMultipleBranches").GetBoolean());

        var branch = snapshot.GetProperty("branches").EnumerateArray()
            .Single(b => b.GetProperty("customerId").GetGuid() == _customerId);

        foreach (var field in new[] { "id", "customerId", "name", "location", "address", "phone", "contactPerson" })
        {
            Assert.True(branch.TryGetProperty(field, out _), $"branch.{field} is missing");
        }

        Assert.Equal("Kundara", branch.GetProperty("name").GetString());
    }

    [Fact]
    public async Task The_body_the_app_sends_for_a_sale_at_a_named_branch_is_accepted()
    {
        var branchId = await MakeShopMultiBranchAsync();
        var saleId = Guid.NewGuid();

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
                    "branchId": "{{branchId}}",
                    "lines": [
                      { "productId": "{{_productId}}", "quantity": 10.0, "unitPrice": 35.0 }
                    ],
                    "pricesAsOf": "2026-09-15T05:00:00.000Z",
                    "notes": null
                  }
                }
              ]
            }
            """);

        var result = response.GetProperty("results")[0];
        Assert.Equal("Accepted", result.GetProperty("outcome").GetString());

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invoice = await db.Invoices.SingleAsync(i => i.Id == result.GetProperty("recordId").GetGuid());
        Assert.Equal(branchId, invoice.BranchId);
    }

    [Fact]
    public async Task A_sale_for_a_multi_branch_customer_with_no_branch_is_rejected_not_dropped()
    {
        await MakeShopMultiBranchAsync();
        var saleId = Guid.NewGuid();

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
                }
              ]
            }
            """);

        var result = response.GetProperty("results")[0];
        Assert.Equal("Rejected", result.GetProperty("outcome").GetString());
        Assert.Contains("branch", result.GetProperty("error").GetString());
    }

    [Fact]
    public async Task A_new_shop_found_on_the_road_is_created_priced_and_billed_in_one_offline_batch()
    {
        // Every id below was made on the phone, with no signal, before the server had heard of any of it.
        var shopId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var response = await SubmitAsync($$"""
            {
              "deviceId": "{{_deviceId}}",
              "items": [
                {
                  "clientRequestId": "{{Guid.NewGuid()}}",
                  "type": "Customer",
                  "recordedAt": "2026-09-23T05:00:00.000Z",
                  "customer": {
                    "id": "{{shopId}}",
                    "name": "Danya Supermarket",
                    "contactPerson": "Manager",
                    "phone": "9847000000",
                    "address": "Kundara",
                    "hasMultipleBranches": true
                  }
                },
                {
                  "clientRequestId": "{{Guid.NewGuid()}}",
                  "type": "CustomerBranch",
                  "recordedAt": "2026-09-23T05:00:01.000Z",
                  "branch": {
                    "id": "{{branchId}}",
                    "customerId": "{{shopId}}",
                    "name": "Kundara",
                    "location": "Kollam",
                    "address": null,
                    "phone": null,
                    "contactPerson": null
                  }
                },
                {
                  "clientRequestId": "{{Guid.NewGuid()}}",
                  "type": "CustomerPrice",
                  "recordedAt": "2026-09-23T05:00:02.000Z",
                  "customerPrice": {
                    "customerId": "{{shopId}}",
                    "productId": "{{_productId}}",
                    "unitPrice": 37.0
                  }
                },
                {
                  "clientRequestId": "{{Guid.NewGuid()}}",
                  "type": "Invoice",
                  "recordedAt": "2026-09-23T05:00:03.000Z",
                  "sale": {
                    "customerId": "{{shopId}}",
                    "branchId": "{{branchId}}",
                    "lines": [
                      { "productId": "{{_productId}}", "quantity": 10.0, "unitPrice": 37.0 }
                    ],
                    "pricesAsOf": "2026-09-23T05:00:02.000Z",
                    "notes": null
                  }
                }
              ]
            }
            """);

        var results = response.GetProperty("results");
        foreach (var result in results.EnumerateArray())
        {
            Assert.Equal("Accepted", result.GetProperty("outcome").GetString());
        }

        // The rate the salesperson set is the rate the bill used, so nothing is flagged.
        Assert.False(results[3].GetProperty("priceMismatch").GetBoolean());

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = await db.Invoices.SingleAsync(i => i.Id == results[3].GetProperty("recordId").GetGuid());

            Assert.Equal(shopId, invoice.CustomerId);
            Assert.Equal(branchId, invoice.BranchId);
            Assert.Equal(370m, invoice.TotalAmount);
            Assert.Equal(0m, (await db.Customers.SingleAsync(c => c.Id == shopId)).OpeningBalance);
        }

        // The office sees who found the shop and who set its rate.
        var office = await OfficeAsync();

        var added = await office.GetFromJsonAsync<JsonElement>("/api/sales/customers?addedBySales=true");
        var shop = Assert.Single(added.EnumerateArray());
        Assert.Equal(shopId, shop.GetProperty("id").GetGuid());
        Assert.Equal("van@test.local", shop.GetProperty("createdByName").GetString());

        var changes = await office.GetFromJsonAsync<JsonElement>(
            $"/api/sales/customer-price-changes?customerId={shopId}&salespersonOnly=true");
        var change = Assert.Single(changes.EnumerateArray());
        Assert.Equal(JsonValueKind.Null, change.GetProperty("previousPrice").ValueKind);
        Assert.Equal(37m, change.GetProperty("newPrice").GetDecimal());
        Assert.Equal("van@test.local", change.GetProperty("changedBy").GetString());
        Assert.True(change.GetProperty("changedBySalesperson").GetBoolean());
    }

    [Fact]
    public async Task Another_branch_of_a_known_shop_goes_under_it_rather_than_becoming_a_new_customer()
    {
        var response = await SubmitAsync($$"""
            {
              "deviceId": "{{_deviceId}}",
              "items": [
                {
                  "clientRequestId": "{{Guid.NewGuid()}}",
                  "type": "CustomerBranch",
                  "recordedAt": "2026-09-23T05:00:00.000Z",
                  "branch": {
                    "id": "{{Guid.NewGuid()}}",
                    "customerId": "{{_customerId}}",
                    "name": "Coimbatore",
                    "location": "Coimbatore",
                    "address": null,
                    "phone": null,
                    "contactPerson": null
                  }
                }
              ]
            }
            """);

        Assert.Equal("Accepted", response.GetProperty("results")[0].GetProperty("outcome").GetString());

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(1, await db.Customers.CountAsync());
        Assert.True((await db.Customers.SingleAsync()).HasMultipleBranches);
        Assert.Equal("Coimbatore", (await db.CustomerBranches.SingleAsync()).Name);
    }

    [Fact]
    public async Task A_salesperson_edit_changes_the_details_but_never_the_opening_balance_or_the_office_notes()
    {
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = await db.Customers.SingleAsync(c => c.Id == _customerId);
            customer.OpeningBalance = 500m;
            customer.Notes = "Pays on the 5th";
            await db.SaveChangesAsync();
        }

        var response = await SubmitAsync(CustomerItem(_customerId, "Kumar Stores", phone: "9000000001"));

        Assert.Equal("Accepted", response.GetProperty("results")[0].GetProperty("outcome").GetString());

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = await db.Customers.SingleAsync(c => c.Id == _customerId);

            Assert.Equal("9000000001", customer.Phone);
            Assert.Equal(500m, customer.OpeningBalance);
            Assert.Equal("Pays on the 5th", customer.Notes);
        }
    }

    [Fact]
    public async Task A_new_shop_with_a_name_the_office_already_has_is_refused()
    {
        var response = await SubmitAsync(CustomerItem(Guid.NewGuid(), "Kumar Stores"));

        var result = response.GetProperty("results")[0];
        Assert.Equal("Rejected", result.GetProperty("outcome").GetString());
        Assert.Contains("already exists", result.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Sending_the_same_new_shop_again_never_makes_a_second_customer()
    {
        var shopId = Guid.NewGuid();

        await SubmitAsync(CustomerItem(shopId, "Anand Bakery"));
        var again = await SubmitAsync(CustomerItem(shopId, "Anand Bakery", phone: "9000000002"));

        Assert.Equal("Accepted", again.GetProperty("results")[0].GetProperty("outcome").GetString());

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Customers.CountAsync(c => c.Name == "Anand Bakery"));
    }

    private string CustomerItem(Guid id, string name, string? phone = null) => $$"""
        {
          "deviceId": "{{_deviceId}}",
          "items": [
            {
              "clientRequestId": "{{Guid.NewGuid()}}",
              "type": "Customer",
              "recordedAt": "2026-09-23T05:00:00.000Z",
              "customer": {
                "id": "{{id}}",
                "name": "{{name}}",
                "contactPerson": null,
                "phone": {{(phone is null ? "null" : $"\"{phone}\"")}},
                "address": null,
                "hasMultipleBranches": false
              }
            }
          ]
        }
        """;

    private async Task<HttpClient> OfficeAsync()
    {
        await _api.CreateUserAsync("office@test.local", Roles.Admin);

        return await _api.SignInAsync("office@test.local");
    }

    /// <summary>Turns the seeded shop into a Danya-Supermarket-style customer with one branch, Kundara.</summary>
    private async Task<Guid> MakeShopMultiBranchAsync()
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var customer = await db.Customers.SingleAsync(c => c.Id == _customerId);
        customer.HasMultipleBranches = true;

        var branch = new CustomerBranch { CustomerId = _customerId, Name = "Kundara" };
        db.CustomerBranches.Add(branch);

        await db.SaveChangesAsync();

        return branch.Id;
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

        foreach (var field in new[] { "clientRequestId", "outcome", "recordId", "error", "priceMismatch", "documentNumber" })
        {
            Assert.True(result.TryGetProperty(field, out _), $"result.{field} is missing");
        }
    }

    [Fact]
    public async Task A_synced_sale_comes_back_with_its_official_number_and_a_retry_with_the_same_one()
    {
        var saleId = Guid.NewGuid();
        var body = $$"""
            {
              "deviceId": "{{_deviceId}}",
              "items": [
                {
                  "clientRequestId": "{{saleId}}",
                  "type": "Invoice",
                  "recordedAt": "2026-09-23T06:30:00.000Z",
                  "sale": {
                    "customerId": "{{_customerId}}",
                    "lines": [ { "productId": "{{_productId}}", "quantity": 10.0, "unitPrice": 35.0 } ],
                    "pricesAsOf": "2026-09-23T05:00:00.000Z",
                    "notes": null
                  }
                }
              ]
            }
            """;

        var first = (await SubmitAsync(body)).GetProperty("results")[0];

        // Numbered by the server when it finalized the sale - the phone never makes one up.
        Assert.Equal("Accepted", first.GetProperty("outcome").GetString());
        Assert.Equal("GP/26-27/000001", first.GetProperty("documentNumber").GetString());

        // The phone lost that answer and sends again: same bill, same number, nothing new.
        var retry = (await SubmitAsync(body)).GetProperty("results")[0];

        Assert.Equal("AlreadyAccepted", retry.GetProperty("outcome").GetString());
        Assert.Equal("GP/26-27/000001", retry.GetProperty("documentNumber").GetString());
    }

    [Fact]
    public async Task A_gst_customer_is_marked_in_the_snapshot_with_its_gstin()
    {
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = await db.Customers.SingleAsync(c => c.Id == _customerId);
            customer.Gstin = "32PQRSX9876K1Z3";
            await db.SaveChangesAsync();
        }

        var snapshot = await _phone.GetFromJsonAsync<JsonElement>("/api/mobile/sync/snapshot");
        var shop = snapshot.GetProperty("customers").EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == _customerId);

        Assert.True(shop.GetProperty("isGstRegistered").GetBoolean());
        Assert.Equal("32PQRSX9876K1Z3", shop.GetProperty("gstin").GetString());
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
