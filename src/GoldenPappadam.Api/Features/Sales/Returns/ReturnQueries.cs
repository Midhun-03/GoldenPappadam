using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;

namespace GoldenPappadam.Api.Features.Sales.Returns;

/// <summary>Filter and order before projecting: SQL cannot order by a projected record.</summary>
public static class ReturnQueries
{
    public static IQueryable<ReturnListItemDto> ProjectList(IQueryable<ReturnNote> notes) =>
        notes.Select(r => new ReturnListItemDto(
            r.Id, r.ReturnNumber, r.ReturnDate, r.CustomerId, r.CustomerName, r.BranchName,
            r.Status, r.Settlement, r.Value, r.CreditAmount));

    public static IQueryable<ReturnDetailDto> ProjectDetail(IQueryable<ReturnNote> notes, AppDbContext db) =>
        notes.Select(r => new ReturnDetailDto(
            r.Id,
            r.ReturnNumber,
            r.ReturnDate,
            r.CustomerId,
            r.CustomerName,
            r.BranchId,
            r.BranchName,
            r.Status,
            r.Settlement,
            r.Value,
            r.CreditAmount,
            db.StockMovements
                .Where(m => m.ReferenceType == StockReferenceType.ReturnNote && m.ReferenceId == r.Id && m.Quantity < 0)
                .Select(m => m.Location!.Name)
                .FirstOrDefault(),
            r.Notes,
            r.CreatedAt,
            db.Users.Where(u => u.Id == r.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
            r.SettledAt,
            r.CancelledAt,
            r.Status == ReturnStatus.Cancelled
                ? db.Users.Where(u => u.Id == r.UpdatedBy).Select(u => u.FullName).FirstOrDefault()
                : null,
            r.CancellationReason,
            r.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new ReturnLineDto(
                    l.Id, l.LineNumber, l.ProductId, l.Description, l.UnitCode, l.Quantity, l.Reason, l.UnitRate, l.Value))
                .ToList()));
}
