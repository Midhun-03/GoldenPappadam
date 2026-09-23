using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Sales.Invoices;

/// <summary>
/// Hands out invoice numbers. The database does the counting, never the application and never a
/// device, so an admin PC and two phones finalizing in the same instant cannot collide.
///
/// How: one row per series per financial year in <c>sales.InvoiceNumberSequences</c>, incremented
/// by a single UPDATE inside the transaction that saves the invoice. The UPDATE takes an exclusive
/// lock on that row and holds it until the transaction ends, so a second finalization waits for
/// the first to commit and then reads the number after it. If the invoice fails to save, the
/// increment rolls back with it - which is why a failed bill never burns a number and the series
/// stays gapless, as GST record-keeping expects.
///
/// Why not MAX(number) + 1: two readers can see the same maximum. Why not a SQL Server SEQUENCE:
/// it is not transactional (a rolled-back bill leaves a gap) and needs one object per year.
/// The UPDATE ... OUTPUT is SQL Server syntax; PostgreSQL spells it UPDATE ... RETURNING, and this
/// is the only place that would change.
/// </summary>
public static class InvoiceNumbering
{
    public record Reserved(string SeriesCode, string FinancialYear, int SequenceNumber, string InvoiceNumber);

    /// <summary>
    /// The Indian financial year runs 1 April to 31 March: 2026-03-31 is in 2025-26 and 2026-04-01
    /// in 2026-27. Derived from the invoice date, so the new year's numbering starts on its own.
    /// </summary>
    public static string FinancialYearOf(DateOnly invoiceDate)
    {
        var startYear = invoiceDate.Month >= 4 ? invoiceDate.Year : invoiceDate.Year - 1;

        return $"{startYear}-{(startYear + 1) % 100:D2}";
    }

    /// <summary>"GP/26-27/000125". Fifteen characters, inside GST's sixteen-character limit.</summary>
    public static string Format(string seriesCode, string financialYear, int sequenceNumber) =>
        $"{seriesCode}/{financialYear[2..]}/{sequenceNumber:D6}";

    /// <summary>Must be called inside the transaction that saves the invoice.</summary>
    public static async Task<Reserved> ReserveAsync(
        AppDbContext db,
        string seriesCode,
        DateOnly invoiceDate,
        CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("An invoice number can only be reserved inside a transaction.");
        }

        var financialYear = FinancialYearOf(invoiceDate);

        // UPDLOCK + HOLDLOCK also locks the key range when the row does not exist yet, so the
        // first two invoices of a new year cannot both insert a counter: the second waits, then
        // finds the row the first created and increments it.
        var next = await db.Database.SqlQuery<int>($"""
            DECLARE @next int;

            UPDATE sales.InvoiceNumberSequences WITH (UPDLOCK, HOLDLOCK)
            SET @next = LastNumber = LastNumber + 1, UpdatedAt = SYSUTCDATETIME()
            WHERE SeriesCode = {seriesCode} AND FinancialYear = {financialYear};

            IF @@ROWCOUNT = 0
            BEGIN
                SET @next = 1;
                INSERT INTO sales.InvoiceNumberSequences (Id, SeriesCode, FinancialYear, LastNumber, CreatedAt)
                VALUES ({Guid.NewGuid()}, {seriesCode}, {financialYear}, 1, SYSUTCDATETIME());
            END

            SELECT @next AS [Value];
            """).ToListAsync(ct);

        var sequenceNumber = next.Single();

        return new Reserved(seriesCode, financialYear, sequenceNumber, Format(seriesCode, financialYear, sequenceNumber));
    }

    /// <summary>
    /// Recovery only, never the way numbers are made: if the counter has somehow fallen behind the
    /// invoices already saved (restored backup, a hand edit), move it past them so the next
    /// reservation succeeds instead of failing on the unique index for ever.
    /// </summary>
    public static async Task RepairAsync(AppDbContext db, string seriesCode, string financialYear, CancellationToken ct)
    {
        await db.Database.ExecuteSqlAsync($"""
            UPDATE s
            SET LastNumber = i.MaxNumber, UpdatedAt = SYSUTCDATETIME()
            FROM sales.InvoiceNumberSequences s
            CROSS APPLY (
                SELECT MAX(SequenceNumber) AS MaxNumber
                FROM sales.Invoices
                WHERE SeriesCode = s.SeriesCode AND FinancialYear = s.FinancialYear
            ) i
            WHERE s.SeriesCode = {seriesCode} AND s.FinancialYear = {financialYear}
              AND i.MaxNumber > s.LastNumber;
            """, ct);
    }
}
