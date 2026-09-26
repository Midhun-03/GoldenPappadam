using GoldenPappadam.Api.Features.Reports;
using GoldenPappadam.Domain.Sales;

namespace GoldenPappadam.Api.Features.Sales.Returns;

/// <summary>
/// A return note laid out as a one-section report, so it prints with the same renderer as the
/// reports. Made from the saved note every time: nothing on it can change except its status.
/// </summary>
public static class ReturnNoteDocument
{
    public static ReportDocument Build(ReturnDetailDto note)
    {
        var shop = note.BranchName is null ? note.CustomerName : $"{note.CustomerName} – {note.BranchName}";
        var settled = note.Settlement switch
        {
            ReturnSettlement.Replacement => $"Replaced free{(note.ReplacementFrom is null ? "" : $" from {note.ReplacementFrom}")}",
            ReturnSettlement.Credit => "Credited to the shop's account",
            ReturnSettlement.NoCompensation => "Nothing given",
            _ => "Office to decide"
        };
        var status = note.Status == ReturnStatus.Cancelled ? $" · CANCELLED: {note.CancellationReason}" : "";

        return new ReportDocument(
            "return-note",
            $"Return note {note.ReturnNumber}",
            ReportPeriodText(note.ReturnDate),
            note.ReturnDate,
            note.ReturnDate,
            $"{shop} · {settled}{status}",
            [
                new ReportFigure("Value of packets", note.Value, ReportColumnKind.Money),
                new ReportFigure("Credited", note.CreditAmount, ReportColumnKind.Money)
            ],
            [
                ReportSection.Create(
                    "Packets returned",
                    [
                        new ReportColumn("line", "#", ReportColumnKind.Count),
                        new ReportColumn("product", "Product", ReportColumnKind.Text),
                        new ReportColumn("reason", "Reason", ReportColumnKind.Text),
                        new ReportColumn("quantity", "Quantity", ReportColumnKind.Quantity, Total: true),
                        new ReportColumn("rate", "Rate", ReportColumnKind.Money),
                        new ReportColumn("value", "Value", ReportColumnKind.Money, Total: true)
                    ],
                    note.Lines.Select(l => Row.Of(
                        ("line", l.LineNumber),
                        ("product", $"{l.Description} ({l.UnitCode})"),
                        ("reason", l.Reason.ToString()),
                        ("quantity", l.Quantity),
                        ("rate", l.UnitRate),
                        ("value", l.Value))),
                    "Returned packets are expired or damaged and are never resold." +
                    (note.Notes is null ? "" : $" Note: {note.Notes}"))
            ],
            DateTime.UtcNow);
    }

    private static string ReportPeriodText(DateOnly date) => ReportPeriod.Describe(date, date);
}
