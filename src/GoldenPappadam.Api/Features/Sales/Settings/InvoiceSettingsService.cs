using System.Text.RegularExpressions;
using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Sales.Invoices;
using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Sales.Settings;

/// <summary>
/// The business details printed on invoices. Changing them affects only bills made afterwards:
/// every invoice keeps its own copy of what it printed.
/// </summary>
public partial class InvoiceSettingsService(AppDbContext db)
{
    [GeneratedRegex("^[A-Z0-9]{1,3}$")]
    private static partial Regex SeriesShape();

    public async Task<InvoiceSettingsDto> GetAsync(CancellationToken ct)
    {
        var settings = await db.InvoiceSettings.AsNoTracking().SingleAsync(s => s.Id == InvoiceSettings.SingletonId, ct);

        return await ToDtoAsync(settings, ct);
    }

    public async Task<InvoiceSettingsDto> SaveAsync(SaveInvoiceSettingsRequest request, CancellationToken ct)
    {
        var stateCode = request.StateCode.Trim();
        var seriesCode = request.SeriesCode.Trim().ToUpperInvariant();
        var gstin = Gstin.Normalise(request.Gstin);

        if (!IndianStates.IsValid(stateCode))
        {
            throw new DomainException("Choose the state the business is registered in.");
        }

        if (!SeriesShape().IsMatch(seriesCode))
        {
            throw new DomainException(
                "The invoice prefix must be one to three letters or digits. GST allows invoice numbers of " +
                "sixteen characters, and GP/26-27/000125 already uses fifteen.");
        }

        EnsureGstinMatchesState(gstin, stateCode, "the business");

        var settings = await db.InvoiceSettings.SingleAsync(s => s.Id == InvoiceSettings.SingletonId, ct);

        settings.LegalName = request.LegalName.Trim();
        settings.Address = Clean(request.Address);
        settings.Phone = Clean(request.Phone);
        settings.Email = Clean(request.Email);
        settings.Gstin = gstin;
        settings.StateCode = stateCode;
        settings.SeriesCode = seriesCode;
        settings.PricesIncludeTax = request.PricesIncludeTax;
        settings.RoundToNearestRupee = request.RoundToNearestRupee;
        settings.PaymentTerms = Clean(request.PaymentTerms);
        settings.BankDetails = Clean(request.BankDetails);
        settings.TermsAndConditions = Clean(request.TermsAndConditions);

        await db.SaveChangesAsync(ct);

        return await ToDtoAsync(settings, ct);
    }

    /// <summary>
    /// Shared with customers and branches: a GSTIN must look like one, and its first two digits
    /// are its state, so it cannot disagree with the state chosen beside it.
    /// </summary>
    public static void EnsureGstinMatchesState(string? gstin, string? stateCode, string whose)
    {
        if (gstin is null)
        {
            return;
        }

        if (!Gstin.IsValid(gstin))
        {
            throw new DomainException(
                $"'{gstin}' is not a valid GSTIN for {whose}. It is 15 characters, like 32ABCDE1234F1Z5.");
        }

        if (stateCode is not null && Gstin.StateCodeOf(gstin) != stateCode)
        {
            throw new DomainException(
                $"The GSTIN for {whose} starts with {Gstin.StateCodeOf(gstin)}, which is " +
                $"{IndianStates.Find(Gstin.StateCodeOf(gstin))?.Name ?? "an unknown state"}, " +
                $"but the state chosen is {IndianStates.Find(stateCode)?.Name ?? stateCode}.");
        }
    }

    private async Task<InvoiceSettingsDto> ToDtoAsync(InvoiceSettings settings, CancellationToken ct)
    {
        var financialYear = InvoiceNumbering.FinancialYearOf(IndiaTime.Today());
        var last = await db.InvoiceNumberSequences
            .Where(s => s.SeriesCode == settings.SeriesCode && s.FinancialYear == financialYear)
            .Select(s => (int?)s.LastNumber)
            .FirstOrDefaultAsync(ct) ?? 0;

        return new InvoiceSettingsDto(
            settings.LegalName,
            settings.Address,
            settings.Phone,
            settings.Email,
            settings.Gstin,
            settings.StateCode,
            settings.SeriesCode,
            settings.PricesIncludeTax,
            settings.RoundToNearestRupee,
            settings.PaymentTerms,
            settings.BankDetails,
            settings.TermsAndConditions,
            settings.Gstin is not null,
            InvoiceNumbering.Format(settings.SeriesCode, financialYear, last + 1),
            settings.UpdatedAt);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
