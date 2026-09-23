using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Sales.Settings;
using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Sales.Customers;

public class CustomerService(AppDbContext db)
{
    /// <summary>
    /// <paramref name="id"/> is supplied by the phone, which names a shop it found offline before the
    /// server has ever heard of it, so a sale for that shop in the same batch can refer to it.
    /// </summary>
    public async Task<Customer> CreateAsync(SaveCustomerRequest request, CancellationToken ct, Guid? id = null)
    {
        await EnsureNameIsFreeAsync(request.Name, null, ct);
        var (gstin, stateCode) = ResolveTaxIdentity(request);
        await EnsureBusinessCanIssueGstBillsAsync(request, wasGstRegistered: false, ct);

        var customer = new Customer
        {
            Id = id ?? Guid.Empty,
            Name = request.Name.Trim(),
            ContactPerson = Clean(request.ContactPerson),
            Phone = Clean(request.Phone),
            Address = Clean(request.Address),
            OpeningBalance = request.OpeningBalance,
            Notes = Clean(request.Notes),
            HasMultipleBranches = request.HasMultipleBranches,
            Email = Clean(request.Email),
            Gstin = gstin,
            StateCode = stateCode
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        return customer;
    }

    public async Task<Customer> UpdateAsync(Guid id, SaveCustomerRequest request, CancellationToken ct)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new NotFoundException("Customer");

        await EnsureNameIsFreeAsync(request.Name, id, ct);
        var (gstin, stateCode) = ResolveTaxIdentity(request);
        await EnsureBusinessCanIssueGstBillsAsync(request, wasGstRegistered: customer.Gstin is not null, ct);

        if (customer.OpeningBalance != request.OpeningBalance &&
            await db.Invoices.AnyAsync(i => i.CustomerId == id, ct))
        {
            throw new DomainException(
                "The opening balance cannot be changed once this customer has bills. " +
                "Record a payment or a new bill instead.");
        }

        if (customer.HasMultipleBranches && !request.HasMultipleBranches &&
            await db.CustomerBranches.AnyAsync(b => b.CustomerId == id && b.IsActive, ct))
        {
            throw new DomainException(
                "This customer still has active branches. Deactivate them first, then turn off multiple branches.");
        }

        customer.Name = request.Name.Trim();
        customer.ContactPerson = Clean(request.ContactPerson);
        customer.Phone = Clean(request.Phone);
        customer.Address = Clean(request.Address);
        customer.OpeningBalance = request.OpeningBalance;
        customer.Notes = Clean(request.Notes);
        customer.HasMultipleBranches = request.HasMultipleBranches;
        customer.Email = Clean(request.Email);
        customer.Gstin = gstin;
        customer.StateCode = stateCode;

        await db.SaveChangesAsync(ct);

        return customer;
    }

    public async Task<Customer> SetActiveAsync(Guid id, bool isActive, CancellationToken ct)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new NotFoundException("Customer");

        customer.IsActive = isActive;
        await db.SaveChangesAsync(ct);

        return customer;
    }

    /// <summary>The account statement: opening balance, then every bill and payment in date order.</summary>
    public async Task<IReadOnlyList<LedgerEntryDto>> GetLedgerAsync(Guid customerId, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct)
                       ?? throw new NotFoundException("Customer");

        var invoices = await db.Invoices
            .Where(i => i.CustomerId == customerId && i.Status == InvoiceStatus.Issued)
            .Select(i => new { i.InvoiceDate, i.InvoiceNumber, i.TotalAmount, i.CreatedAt })
            .ToListAsync(ct);

        var payments = await db.Payments
            .Where(p => p.CustomerId == customerId)
            .Select(p => new { p.PaymentDate, p.Amount, p.Method, p.Reference, p.CreatedAt })
            .ToListAsync(ct);

        var entries = invoices
            .Select(i => (
                Date: i.InvoiceDate,
                i.CreatedAt,
                Entry: new LedgerEntryDto(i.InvoiceDate, "Invoice", i.InvoiceNumber, null, i.TotalAmount, 0m, 0m)))
            .Concat(payments.Select(p => (
                Date: p.PaymentDate,
                p.CreatedAt,
                Entry: new LedgerEntryDto(
                    p.PaymentDate,
                    "Payment",
                    p.Reference ?? p.Method.ToString(),
                    p.Method.ToString(),
                    0m,
                    p.Amount,
                    0m))))
            .OrderBy(x => x.Date)
            .ThenBy(x => x.CreatedAt)
            .Select(x => x.Entry)
            .ToList();

        var ledger = new List<LedgerEntryDto>();
        var balance = customer.OpeningBalance;

        if (customer.OpeningBalance != 0m)
        {
            var openingDate = entries.Count > 0 ? entries[0].Date : DateOnly.FromDateTime(DateTime.UtcNow);
            ledger.Add(new LedgerEntryDto(
                openingDate, "Opening", "Opening balance", null, customer.OpeningBalance, 0m, balance));
        }

        foreach (var entry in entries)
        {
            balance += entry.Billed - entry.Paid;
            ledger.Add(entry with { Balance = balance });
        }

        return ledger;
    }

    /// <summary>Bills with money still on them, oldest first — the order payments are applied in.</summary>
    public async Task<IReadOnlyList<OutstandingInvoiceDto>> GetOutstandingInvoicesAsync(
        Guid customerId,
        CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct))
        {
            throw new NotFoundException("Customer");
        }

        var rows = await db.Invoices
            .Where(i => i.CustomerId == customerId && i.Status == InvoiceStatus.Issued)
            .OrderBy(i => i.InvoiceDate)
            .ThenBy(i => i.InvoiceNumber)
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.InvoiceDate,
                i.TotalAmount,
                Paid = db.PaymentAllocations
                    .Where(a => a.InvoiceId == i.Id)
                    .Sum(a => (decimal?)a.Amount) ?? 0m
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new OutstandingInvoiceDto(
                r.Id, r.InvoiceNumber, r.InvoiceDate, r.TotalAmount, r.Paid, r.TotalAmount - r.Paid))
            .Where(r => r.Outstanding > 0m)
            .ToList();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task EnsureNameIsFreeAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var trimmed = name.Trim();
        var taken = await db.Customers.AnyAsync(c => c.Name == trimmed && (exceptId == null || c.Id != exceptId), ct);

        if (taken)
        {
            throw new DomainException($"A customer named '{trimmed}' already exists.");
        }
    }

    /// <summary>
    /// A GST customer's bills carry Golden Pappadam's GSTIN too, so a shop cannot be made a GST
    /// customer before that is entered - otherwise the salesman's next sale there would be saved on
    /// the phone and then refused at sync. A shop that is already one keeps working either way.
    /// </summary>
    private async Task EnsureBusinessCanIssueGstBillsAsync(
        SaveCustomerRequest request,
        bool wasGstRegistered,
        CancellationToken ct)
    {
        if (request.IsGstRegistered && !wasGstRegistered &&
            !await db.InvoiceSettings.AnyAsync(s => s.Gstin != null, ct))
        {
            throw new DomainException(
                "Enter Golden Pappadam's GSTIN in Settings before marking shops GST registered: a GST bill " +
                "carries both GSTINs.");
        }
    }

    /// <summary>
    /// A GST customer must give its GSTIN, and a normal customer must not have one: the tick box and
    /// the number have to agree, because the GSTIN is what decides whether the shop gets GST bills.
    /// A GSTIN carries its state in its first two digits, so a registered shop with no state picked
    /// gets it from the GSTIN, and one whose picked state disagrees is refused.
    /// </summary>
    private static (string? Gstin, string? StateCode) ResolveTaxIdentity(SaveCustomerRequest request)
    {
        var gstin = Gstin.Normalise(request.Gstin);
        var stateCode = Clean(request.StateCode);

        if (request.IsGstRegistered && gstin is null)
        {
            throw new DomainException(
                $"Enter the GSTIN for {request.Name.Trim()}, or untick GST registered to give it normal bills.");
        }

        if (!request.IsGstRegistered && gstin is not null)
        {
            throw new DomainException(
                $"Tick GST registered to save a GSTIN for {request.Name.Trim()}. Only GST customers get GST bills.");
        }

        if (stateCode is not null && !IndianStates.IsValid(stateCode))
        {
            throw new DomainException($"'{stateCode}' is not a GST state code.");
        }

        InvoiceSettingsService.EnsureGstinMatchesState(gstin, stateCode, request.Name.Trim());

        return (gstin, stateCode ?? (gstin is null ? null : Gstin.StateCodeOf(gstin)));
    }
}
