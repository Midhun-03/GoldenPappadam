using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Reports;

/// <summary>
/// Stock for a day or a period, per product and place: what there was, everything that happened to
/// it, and what is left. It is the ledger's own arithmetic, so it always agrees with the stock screen.
/// Movements out are minus figures, so every row adds up: opening + each column = closing.
/// </summary>
public class StockMovementReport(AppDbContext db)
{
    private static readonly (string Key, string Title, Func<StockMovementType, bool> Type, int Sign)[] Columns =
    [
        ("made", "Made / packed", t => t is StockMovementType.Production or StockMovementType.Opening or StockMovementType.Packing, 1),
        ("packedOut", "Used for packing", t => t == StockMovementType.Packing, -1),
        ("repackedIn", "Repacked in", t => t == StockMovementType.Repacking, 1),
        ("repackedOut", "Repacked out", t => t == StockMovementType.Repacking, -1),
        // Sent to the own shop counts too: kg out of the warehouse, pieces into the shop.
        ("movedIn", "Moved in", t => t is StockMovementType.Transfer or StockMovementType.ShopTransfer, 1),
        ("movedOut", "Moved out", t => t is StockMovementType.Transfer or StockMovementType.ShopTransfer, -1),
        ("sold", "Sold", t => t is StockMovementType.Sale or StockMovementType.SaleReversal, 0),
        ("replaced", "Replaced free", t => t == StockMovementType.Replacement, 0),
        ("damaged", "Damaged / expired", t => t == StockMovementType.Damage, 0),
        ("adjusted", "Counted / adjusted", t => t == StockMovementType.Adjustment, 0)
    ];

    public async Task<ReportDocument> BuildAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var start = IndiaTime.DayRangeUtc(from).Start;
        var end = IndiaTime.DayRangeUtc(to).End;

        var opening = await db.StockMovements
            .Where(m => m.OccurredAt < start)
            .GroupBy(m => new { m.ProductId, m.LocationId })
            .Select(g => new { g.Key.ProductId, g.Key.LocationId, Quantity = g.Sum(m => m.Quantity) })
            .ToListAsync(ct);

        var during = await db.StockMovements
            .Where(m => m.OccurredAt >= start && m.OccurredAt < end)
            .GroupBy(m => new { m.ProductId, m.LocationId, m.MovementType })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.LocationId,
                g.Key.MovementType,
                In = g.Sum(m => m.Quantity > 0 ? m.Quantity : 0m),
                Out = g.Sum(m => m.Quantity < 0 ? m.Quantity : 0m)
            })
            .ToListAsync(ct);

        var products = await db.Products
            .Select(p => new { p.Id, p.Name, Unit = p.UnitOfMeasure!.Code })
            .ToDictionaryAsync(p => p.Id, ct);
        var locations = await db.StockLocations.OrderBy(l => l.Code).ToListAsync(ct);

        var sections = new List<ReportSection>();

        foreach (var location in locations.OrderBy(l => l.Id == KnownStockLocations.MainWarehouseId ? 0 : 1).ThenBy(l => l.Code))
        {
            var keys = opening.Where(o => o.LocationId == location.Id).Select(o => o.ProductId)
                .Concat(during.Where(d => d.LocationId == location.Id).Select(d => d.ProductId))
                .Distinct()
                .Where(products.ContainsKey)
                .OrderBy(id => products[id].Name);

            var rows = new List<IReadOnlyDictionary<string, object?>>();

            foreach (var productId in keys)
            {
                var start0 = opening.FirstOrDefault(o => o.ProductId == productId && o.LocationId == location.Id)?.Quantity ?? 0m;
                var mine = during.Where(d => d.ProductId == productId && d.LocationId == location.Id).ToList();

                var cells = new List<(string, object?)>
                {
                    ("product", $"{products[productId].Name} ({products[productId].Unit})"),
                    ("opening", start0)
                };

                foreach (var column in Columns)
                {
                    var value = mine.Where(d => column.Type(d.MovementType))
                        .Sum(d => column.Sign switch { 1 => d.In, -1 => d.Out, _ => d.In + d.Out });
                    cells.Add((column.Key, value == 0m ? null : value));
                }

                var closing = start0 + mine.Sum(d => d.In + d.Out);

                if (start0 == 0m && closing == 0m && mine.Count == 0)
                {
                    continue;
                }

                cells.Add(("closing", closing));
                rows.Add(Row.Of([.. cells]));
            }

            if (rows.Count == 0)
            {
                continue;
            }

            var columns = new List<ReportColumn>
            {
                new("product", "Product", ReportColumnKind.Text),
                new("opening", "Opening", ReportColumnKind.Quantity)
            };
            columns.AddRange(Columns.Select(c => new ReportColumn(c.Key, c.Title, ReportColumnKind.Quantity)));
            columns.Add(new ReportColumn("closing", "Closing", ReportColumnKind.Quantity));

            sections.Add(ReportSection.Create(
                location.Name,
                columns,
                rows,
                "Movements out are minus figures, so each row adds up: opening + every column = closing. " +
                "\"Sold\" is net of cancelled bills."));
        }

        return new ReportDocument(
            "stock",
            "Stock report",
            ReportPeriod.Describe(from, to),
            from,
            to,
            null,
            [],
            sections,
            DateTime.UtcNow);
    }
}
