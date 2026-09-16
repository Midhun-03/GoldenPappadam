using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.FieldSales.StockRequests;
using GoldenPappadam.Api.Features.FieldSales.VanLoads;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Api.Features.Sales.Customers;
using GoldenPappadam.Api.Features.Sales.Invoices;
using GoldenPappadam.Api.Features.Sales.Payments;
using GoldenPappadam.Domain.FieldSales;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Identity;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Mobile;

/// <summary>
/// The bridge between the phone and the system. It deliberately owns no business rules: a sale
/// goes through the same <see cref="InvoiceService"/> the admin panel uses and a payment through
/// the same <see cref="PaymentService"/>, so there is exactly one copy of how money and stock work.
///
/// What it does own is the promise that makes offline safe - sending the same thing twice creates
/// one record, and a rejection never loses what the salesperson did.
/// </summary>
public class MobileSyncService(
    AppDbContext db,
    ICurrentUser currentUser,
    InvoiceService invoices,
    PaymentService payments,
    CustomerPriceService prices,
    VanLoadService vanLoads,
    StockRequestService stockRequests)
{
    private static readonly int[] UniqueViolationErrors = [2601, 2627];

    /// <summary>How much payment history the phone carries. Enough to answer a doorway question.</summary>
    private const int RecentPaymentDays = 90;

    // ---------- device ----------

    public async Task<DeviceDto> RegisterAsync(RegisterDeviceRequest request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new DomainException("Sign in first.");
        var name = request.Name.Trim();

        // Re-registering the same handset updates it rather than piling up rows.
        var device = await db.Devices.FirstOrDefaultAsync(d => d.UserId == userId && d.Name == name, ct);

        if (device is null)
        {
            device = new Device
            {
                UserId = userId,
                Name = name,
                Platform = request.Platform.Trim(),
                LastSeenAt = DateTime.UtcNow
            };
            db.Devices.Add(device);
        }
        else
        {
            device.Platform = request.Platform.Trim();
            device.LastSeenAt = DateTime.UtcNow;
            device.IsActive = true;
        }

        await db.SaveChangesAsync(ct);

        return await DescribeDeviceAsync(device.Id, ct);
    }

    public async Task<DeviceDto> DescribeDeviceAsync(Guid deviceId, CancellationToken ct) =>
        await db.Devices
            .Where(d => d.Id == deviceId)
            .Select(d => new DeviceDto(d.Id, d.Name, d.Platform, d.LocationId, d.Location!.Code))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Device");

    // ---------- snapshot ----------

    public async Task<SnapshotDto> GetSnapshotAsync(CancellationToken ct)
    {
        var customers = await CustomerQueries.Project(db.Customers.Where(c => c.IsActive).OrderBy(c => c.Name), db)
            .Select(c => new SnapshotCustomerDto(c.Id, c.Name, c.ContactPerson, c.Phone, c.Address, c.Balance))
            .ToListAsync(ct);

        var products = await db.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new SnapshotProductDto(
                p.Id, p.ProductCode, p.Name, p.UnitOfMeasure!.Code, p.SellingPrice))
            .ToListAsync(ct);

        var priceRows = await db.CustomerPrices
            .Where(cp => cp.IsActive)
            .Select(cp => new SnapshotPriceDto(cp.CustomerId, cp.ProductId, cp.UnitPrice))
            .ToListAsync(ct);

        // The newest price change the phone is being told about. It sends this back with a sale,
        // and anything changed after it is what makes a mismatch detectable.
        var pricesAsOf = await db.CustomerPrices
            .Select(cp => cp.UpdatedAt ?? cp.CreatedAt)
            .OrderByDescending(at => at)
            .FirstOrDefaultAsync(ct);

        // Recent only. The shop page answers "when did I last collect from you?", not "show me
        // three years of history", and the phone should not carry what it will never display.
        var since = IndiaTime.Today().AddDays(-RecentPaymentDays);

        var recentPayments = await db.Payments
            .Where(p => p.PaymentDate >= since)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .Select(p => new SnapshotPaymentDto(
                p.Id,
                p.CustomerId,
                p.PaymentDate,
                p.CreatedAt,
                p.Amount,
                p.Method.ToString(),
                p.Reference,
                p.Notes))
            .ToListAsync(ct);

        var device = await CurrentDeviceAsync(ct);

        return new SnapshotDto(
            DateTime.UtcNow,
            pricesAsOf == default ? DateTime.UtcNow : pricesAsOf,
            device?.LocationId,
            customers,
            products,
            priceRows,
            recentPayments,
            Enum.GetNames<PaymentMethod>());
    }

    // ---------- upload ----------

    /// <summary>
    /// One item at a time, each in its own transaction, so a single rejected sale never stops the
    /// nine good ones behind it. The phone gets an answer per item and decides what to keep.
    /// </summary>
    public async Task<SubmissionBatchResponse> SubmitAsync(SubmissionBatchRequest request, CancellationToken ct)
    {
        var device = await db.Devices.FirstOrDefaultAsync(d => d.Id == request.DeviceId, ct)
                     ?? throw new NotFoundException("Device");

        if (!device.IsActive)
        {
            throw new DomainException("This device has been deactivated. Ask the office about it.");
        }

        if (device.UserId != currentUser.UserId)
        {
            throw new DomainException("That device belongs to someone else.");
        }

        var results = new List<SubmissionResultDto>();

        foreach (var item in request.Items)
        {
            results.Add(await SubmitOneAsync(device, item, ct));
        }

        device.LastSeenAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new SubmissionBatchResponse(DateTime.UtcNow, results);
    }

    private async Task<SubmissionResultDto> SubmitOneAsync(
        Device device,
        SubmissionItemRequest item,
        CancellationToken ct)
    {
        // Already here? Then this is a retry of something that worked, and the honest answer is
        // the record it made the first time.
        var existing = await FindSubmissionAsync(item.ClientRequestId, ct);

        if (existing is not null)
        {
            return new SubmissionResultDto(
                item.ClientRequestId, SubmissionOutcome.AlreadyAccepted, existing.CreatedRecordId,
                null, existing.PriceMismatch, []);
        }

        try
        {
            return item.Type switch
            {
                SubmissionType.Invoice => await AcceptSaleAsync(device, item, ct),
                SubmissionType.Payment => await AcceptPaymentAsync(device, item, ct),
                SubmissionType.Visit => await AcceptVisitAsync(device, item, ct),
                SubmissionType.VanLoad => await AcceptVanLoadAsync(device, item, ct),
                SubmissionType.StockRequest => await AcceptStockRequestAsync(device, item, ct),
                _ => Rejected(item, $"{item.Type} is not something this device can send.")
            };
        }
        catch (DomainException exception)
        {
            // NotFoundException is a DomainException, so this covers "no such shop" too. Either
            // way the phone keeps the row and shows the salesperson what the server objected to.
            return Rejected(item, exception.Message);
        }
        catch (DbUpdateException exception) when (IsDuplicate(exception))
        {
            // Two syncs raced. The unique index settled it; report whichever won.
            db.ChangeTracker.Clear();
            var winner = await FindSubmissionAsync(item.ClientRequestId, ct);

            return new SubmissionResultDto(
                item.ClientRequestId, SubmissionOutcome.AlreadyAccepted, winner?.CreatedRecordId,
                null, winner?.PriceMismatch ?? false, []);
        }
    }

    private async Task<SubmissionResultDto> AcceptSaleAsync(
        Device device,
        SubmissionItemRequest item,
        CancellationToken ct)
    {
        var sale = item.Sale ?? throw new DomainException("This submission says it is a sale but carries none.");

        // A bill follows the goods, and the goods come off the van. With no van there is nothing to
        // have delivered, so the bill is refused rather than quietly drawn from the warehouse -
        // which would balance the books and leave the van's wrong.
        var vanId = RequireVan(device);

        // The price the phone charged is kept as charged. What the server decides is whether the
        // office has changed it since, in which case a person is asked to look.
        var mismatch = await HasPriceMovedAsync(sale, ct);

        var created = await invoices.CreateAsync(
            new CreateInvoiceRequest(
                sale.CustomerId,
                IndiaTime.ToIndiaDate(item.RecordedAt),
                0m,
                sale.Notes,
                sale.Lines.Select(l => new InvoiceLineRequest(l.ProductId, l.Quantity, l.UnitPrice)).ToList(),
                vanId),
            ct);

        await RecordSubmissionAsync(device, item, created.Invoice.Id, mismatch, ct);

        return new SubmissionResultDto(
            item.ClientRequestId, SubmissionOutcome.Accepted, created.Invoice.Id, null, mismatch, created.Warnings);
    }

    private async Task<SubmissionResultDto> AcceptPaymentAsync(
        Device device,
        SubmissionItemRequest item,
        CancellationToken ct)
    {
        var payment = item.Payment
                      ?? throw new DomainException("This submission says it is a payment but carries none.");

        // Oldest bill first, which is how bill-to-bill settlement already works on the road.
        var created = await payments.CreateAsync(
            new CreatePaymentRequest(
                payment.CustomerId,
                IndiaTime.ToIndiaDate(item.RecordedAt),
                payment.Amount,
                payment.Method,
                payment.Reference,
                payment.Notes,
                null),
            ct);

        await RecordSubmissionAsync(device, item, created.Payment.Id, false, ct);

        return new SubmissionResultDto(
            item.ClientRequestId, SubmissionOutcome.Accepted, created.Payment.Id, null, false, []);
    }

    private async Task<SubmissionResultDto> AcceptVisitAsync(
        Device device,
        SubmissionItemRequest item,
        CancellationToken ct)
    {
        var visit = item.Visit ?? throw new DomainException("This submission says it is a visit but carries none.");

        if (!await db.Customers.AnyAsync(c => c.Id == visit.CustomerId, ct))
        {
            throw new NotFoundException("Customer");
        }

        var shopVisit = new ShopVisit
        {
            CustomerId = visit.CustomerId,
            DeviceId = device.Id,
            VisitedAt = item.RecordedAt.ToUniversalTime(),
            Outcome = visit.Outcome,
            // The phone names the sale and payment by their own ids, because when it saved the
            // visit it had never spoken to the server and knew no other name for them.
            InvoiceId = await ResolveRecordAsync(visit.SaleClientRequestId, SubmissionType.Invoice, ct),
            PaymentId = await ResolveRecordAsync(visit.PaymentClientRequestId, SubmissionType.Payment, ct),
            Notes = visit.Notes
        };

        db.ShopVisits.Add(shopVisit);
        await db.SaveChangesAsync(ct);

        await RecordSubmissionAsync(device, item, shopVisit.Id, false, ct);

        return new SubmissionResultDto(item.ClientRequestId, SubmissionOutcome.Accepted, shopVisit.Id, null, false, []);
    }

    /// <summary>
    /// The stock the salesperson took from the warehouse this morning.
    ///
    /// The van and the warehouse are decided here, from the device, not from anything the phone
    /// sent - so the only stock movement a salesperson can cause is warehouse to their own van.
    /// Both the office and the salesperson may record a load: the packing book says what was
    /// packed, and the salesman writes down what he actually took, which is often less.
    /// </summary>
    private async Task<SubmissionResultDto> AcceptVanLoadAsync(
        Device device,
        SubmissionItemRequest item,
        CancellationToken ct)
    {
        var load = item.VanLoad
                   ?? throw new DomainException("This submission says it is a van load but carries none.");

        var vanId = RequireVan(device);

        var created = await vanLoads.CreateAsync(
            new CreateVanLoadRequest(
                vanId,
                VanLoadDirection.Loading,
                item.RecordedAt,
                load.Notes,
                load.Lines.Select(l => new VanLoadLineRequest(l.ProductId, l.Quantity)).ToList()),
            ct,
            device.Id);

        await RecordSubmissionAsync(device, item, created.VanLoad.Id, false, ct);

        return new SubmissionResultDto(
            item.ClientRequestId, SubmissionOutcome.Accepted, created.VanLoad.Id, null, false,
            created.Warnings);
    }

    /// <summary>
    /// What the salesperson wants packed. No money, no stock and no reservation - it only replaces
    /// telling somebody in person what tomorrow needs to look like.
    /// </summary>
    private async Task<SubmissionResultDto> AcceptStockRequestAsync(
        Device device,
        SubmissionItemRequest item,
        CancellationToken ct)
    {
        var request = item.StockRequest
                      ?? throw new DomainException("This submission says it is a stock request but carries none.");

        var created = await stockRequests.CreateAsync(
            new CreateStockRequest(
                request.RequiredDate,
                request.Lines.Select(l => new StockRequestLineRequest(l.ProductId, l.Quantity)).ToList(),
                request.Notes),
            device.Id,
            ct);

        await RecordSubmissionAsync(device, item, created.Id, false, ct);

        return new SubmissionResultDto(
            item.ClientRequestId, SubmissionOutcome.Accepted, created.Id, null, false, []);
    }

    /// <summary>
    /// The van this phone sells from, or a refusal. Which van a phone belongs to is the office's
    /// decision, deliberately: the server reads it from the device rather than trusting anything the
    /// phone sends, so a phone the office has not placed cannot touch stock at all.
    /// </summary>
    private static Guid RequireVan(Device device) =>
        device.LocationId
        ?? throw new DomainException("This phone is not assigned to a van yet. Ask the office to set that up.");

    /// <summary>What is on this phone's van: loaded today, sold, and what is left.</summary>
    public async Task<VanReconciliationDto> GetVanStockAsync(DateOnly businessDate, CancellationToken ct)
    {
        var device = await CurrentDeviceAsync(ct)
                     ?? throw new NotFoundException("Device");

        return await vanLoads.GetReconciliationAsync(RequireVan(device), businessDate, ct);
    }

    /// <summary>
    /// True when the office has changed what this shop pays since the snapshot the phone priced
    /// from. The sale is still saved at the price it was made at - see docs B6.
    /// </summary>
    private async Task<bool> HasPriceMovedAsync(MobileSaleRequest sale, CancellationToken ct)
    {
        var agreed = await prices.GetAgreedPricesAsync(sale.CustomerId, sale.Lines.Select(l => l.ProductId), ct);

        var productPrices = await db.Products
            .Where(p => sale.Lines.Select(l => l.ProductId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.SellingPrice, ct);

        foreach (var line in sale.Lines)
        {
            var current = CustomerPriceService.Resolve(
                null,
                agreed.TryGetValue(line.ProductId, out var shopPrice) ? shopPrice : null,
                productPrices.GetValueOrDefault(line.ProductId));

            if (current is not null && current != line.UnitPrice)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<Guid?> ResolveRecordAsync(Guid? clientRequestId, SubmissionType type, CancellationToken ct)
    {
        if (clientRequestId is not { } id)
        {
            return null;
        }

        var submission = await FindSubmissionAsync(id, ct);

        // Silently null rather than failing: a visit is worth keeping even if its sale has not
        // landed yet, and losing the stop would be the worse outcome.
        return submission?.SubmissionType == type ? submission.CreatedRecordId : null;
    }

    /// <summary>
    /// Marks the client request as done. This is written just after the record it describes rather
    /// than inside the same transaction, because InvoiceService owns a transaction of its own that
    /// it rolls back and retries when two bills race for an invoice number, and nesting the two
    /// would make that retry tear down the outer one.
    ///
    /// The window that leaves is a process crash between the two commits, which would let a retry
    /// make a second bill. A dropped connection - the failure that actually happens on the road -
    /// is fully covered, because the submission row is committed before the phone is answered.
    /// Closing the crash window properly means letting the sales services join a caller's
    /// transaction, which is worth doing if it ever bites.
    /// </summary>
    private async Task RecordSubmissionAsync(
        Device device,
        SubmissionItemRequest item,
        Guid recordId,
        bool priceMismatch,
        CancellationToken ct)
    {
        db.SyncSubmissions.Add(new SyncSubmission
        {
            ClientRequestId = item.ClientRequestId,
            DeviceId = device.Id,
            SubmissionType = item.Type,
            RecordedAt = item.RecordedAt.ToUniversalTime(),
            ReceivedAt = DateTime.UtcNow,
            CreatedRecordId = recordId,
            PriceMismatch = priceMismatch
        });

        await db.SaveChangesAsync(ct);
    }

    // ---------- the salesperson's own day ----------

    public async Task<MobileDayDto> GetDayAsync(DateOnly businessDate, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new DomainException("Sign in first.");
        var (dayStart, dayEnd) = IndiaTime.DayRangeUtc(businessDate);

        var sales = await db.SyncSubmissions
            .Where(s => s.SubmissionType == SubmissionType.Invoice)
            .Where(s => s.Device!.UserId == userId)
            .Where(s => s.RecordedAt >= dayStart && s.RecordedAt < dayEnd)
            .Join(db.Invoices, s => s.CreatedRecordId, i => i.Id, (s, i) => new { Submission = s, Invoice = i })
            .Where(x => x.Invoice.Status == InvoiceStatus.Issued)
            .OrderByDescending(x => x.Submission.RecordedAt)
            .Select(x => new MobileDaySaleDto(
                x.Invoice.Id,
                x.Invoice.InvoiceNumber,
                x.Invoice.Customer!.Name,
                x.Submission.RecordedAt,
                x.Invoice.TotalAmount))
            .ToListAsync(ct);

        var cash = await db.SyncSubmissions
            .Where(s => s.SubmissionType == SubmissionType.Payment)
            .Where(s => s.Device!.UserId == userId)
            .Where(s => s.RecordedAt >= dayStart && s.RecordedAt < dayEnd)
            .Join(db.Payments, s => s.CreatedRecordId, p => p.Id, (_, p) => p.Amount)
            .SumAsync(amount => (decimal?)amount, ct) ?? 0m;

        var visits = await db.ShopVisits
            .Where(v => v.Device!.UserId == userId)
            .Where(v => v.VisitedAt >= dayStart && v.VisitedAt < dayEnd)
            .Select(v => new { v.CustomerId, v.Outcome })
            .ToListAsync(ct);

        // Delivered today and not settled on the spot. Read off the allocations rather than the
        // day's payments, because money collected today may be settling last week's bills.
        var creditSales = await db.SyncSubmissions
            .Where(s => s.SubmissionType == SubmissionType.Invoice)
            .Where(s => s.Device!.UserId == userId)
            .Where(s => s.RecordedAt >= dayStart && s.RecordedAt < dayEnd)
            .Join(db.Invoices, s => s.CreatedRecordId, i => i.Id, (_, i) => i)
            .Where(i => i.Status == InvoiceStatus.Issued)
            .Select(i => i.TotalAmount -
                         (db.PaymentAllocations.Where(a => a.InvoiceId == i.Id).Sum(a => (decimal?)a.Amount) ?? 0m))
            .SumAsync(outstanding => (decimal?)outstanding, ct) ?? 0m;

        var outstanding = await CustomerQueries
            .Project(db.Customers.Where(c => c.IsActive), db)
            .Select(c => c.Balance)
            .ToListAsync(ct);

        return new MobileDayDto(
            businessDate,
            sales.Sum(s => s.TotalAmount),
            sales.Count,
            visits.Select(v => v.CustomerId).Distinct().Count(),
            cash,
            creditSales,
            visits.Count(v => v.Outcome != VisitOutcome.Sold),
            outstanding.Where(balance => balance > 0m).Sum(),
            sales);
    }

    private Task<SyncSubmission?> FindSubmissionAsync(Guid clientRequestId, CancellationToken ct) =>
        db.SyncSubmissions.AsNoTracking().FirstOrDefaultAsync(s => s.ClientRequestId == clientRequestId, ct);

    private async Task<Device?> CurrentDeviceAsync(CancellationToken ct) =>
        await db.Devices
            .Where(d => d.UserId == currentUser.UserId && d.IsActive)
            .OrderByDescending(d => d.LastSeenAt)
            .FirstOrDefaultAsync(ct);

    private static SubmissionResultDto Rejected(SubmissionItemRequest item, string error) =>
        new(item.ClientRequestId, SubmissionOutcome.Rejected, null, error, false, []);

    private static bool IsDuplicate(DbUpdateException exception) =>
        exception.InnerException is SqlException sql && UniqueViolationErrors.Contains(sql.Number);
}
