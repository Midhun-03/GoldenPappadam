using GoldenPappadam.Api.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static GoldenPappadam.Api.Common.PdfStyle;

namespace GoldenPappadam.Api.Features.Reports;

/// <summary>
/// Prints any <see cref="ReportDocument"/> on A4 in the house style: the business header, the
/// summary figures, then each table with its totals row. A report with a wide table turns the page
/// to landscape rather than squeezing the columns.
/// </summary>
public static class ReportPdfRenderer
{
    private const int LandscapeAbove = 7;

    public record Business(string Name, string? Address);

    public static byte[] Render(ReportDocument report, Business business) => Compose(report, business).GeneratePdf();

    /// <summary>The document itself, for anything other than a PDF - page images in a test, say.</summary>
    public static IDocument Compose(ReportDocument report, Business business)
    {
        UseCommunityLicense();

        var landscape = report.Sections.Any(s => s.Columns.Count > LandscapeAbove);

        return Document.Create(container => container.Page(page =>
            {
                page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(26);
                page.DefaultTextStyle(style => style.FontSize(8.5f).FontColor(Ink));

                page.Header().Element(c => Header(c, report, business));
                page.Content().PaddingTop(10).Column(column =>
                {
                    column.Spacing(14);

                    if (report.Summary.Count > 0)
                    {
                        column.Item().Element(c => Summary(c, report.Summary));
                    }

                    foreach (var section in report.Sections)
                    {
                        column.Item().Element(c => Section(c, section));
                    }
                });
                page.Footer().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Text($"Generated {IndiaTime.Now():dd-MM-yyyy hh:mm tt} IST")
                        .FontSize(7).FontColor(Muted);
                    row.RelativeItem().AlignRight().Text(text =>
                    {
                        text.DefaultTextStyle(s => s.FontSize(7).FontColor(Muted));
                        text.Span($"{report.Title} · Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });
            }))
            .WithMetadata(new DocumentMetadata { Title = $"{report.Title} {report.Period}", Author = business.Name });
    }

    private static void Header(IContainer container, ReportDocument report, Business business)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(30).Height(30).Svg(Logo);
                row.RelativeItem().PaddingLeft(8).Column(name =>
                {
                    name.Item().Text(business.Name.ToUpperInvariant()).FontSize(12).Bold().LetterSpacing(0.04f);
                    if (business.Address is not null)
                    {
                        name.Item().Text(business.Address).FontSize(7.5f).FontColor(Muted);
                    }
                });
                row.RelativeItem().AlignRight().Column(title =>
                {
                    title.Item().AlignRight().Text(report.Title).FontSize(13).Bold().FontColor(Accent);
                    title.Item().AlignRight().Text(report.Period).FontColor(Muted);
                });
            });

            if (report.Subtitle is not null)
            {
                column.Item().PaddingTop(6).Text(report.Subtitle);
            }

            column.Item().PaddingTop(6).LineHorizontal(1.2f).LineColor(Accent);
        });
    }

    private static void Summary(IContainer container, IReadOnlyList<ReportFigure> figures)
    {
        container.Row(row =>
        {
            row.Spacing(8);

            foreach (var figure in figures)
            {
                row.RelativeItem().Border(0.75f).BorderColor(Rule).Padding(7).Column(cell =>
                {
                    cell.Item().Text(figure.Label).FontSize(7.5f).FontColor(Muted);
                    cell.Item().Text(Format(figure.Value, figure.Kind)).FontSize(11).SemiBold();
                });
            }
        });
    }

    private static void Section(IContainer container, ReportSection section)
    {
        container.Column(column =>
        {
            column.Item().PaddingBottom(4).Text(section.Title).FontSize(10).SemiBold();

            if (section.Rows.Count == 0)
            {
                column.Item().Text("Nothing in this period.").FontColor(Muted);
                return;
            }

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    foreach (var definition in section.Columns)
                    {
                        columns.RelativeColumn(Weight(definition, section.Columns));
                    }
                });

                table.Header(header =>
                {
                    foreach (var definition in section.Columns)
                    {
                        var cell = header.Cell().Background(HeaderFill).BorderBottom(0.75f).BorderColor(Rule)
                            .PaddingVertical(4).PaddingHorizontal(3);
                        (IsNumber(definition) ? cell.AlignRight() : cell).Text(definition.Title).FontSize(7.5f).SemiBold();
                    }
                });

                foreach (var row in section.Rows)
                {
                    foreach (var definition in section.Columns)
                    {
                        var cell = table.Cell().BorderBottom(0.5f).BorderColor(Rule).PaddingVertical(3).PaddingHorizontal(3);
                        var value = row.TryGetValue(definition.Key, out var v) ? v : null;
                        (IsNumber(definition) ? cell.AlignRight() : cell).Text(FormatCell(value, definition.Kind));
                    }
                }

                if (section.Totals is not null)
                {
                    var first = true;

                    foreach (var definition in section.Columns)
                    {
                        var cell = table.Cell().BorderTop(1).BorderColor(Ink).PaddingVertical(4).PaddingHorizontal(3);
                        var text = section.Totals.TryGetValue(definition.Key, out var total)
                            ? FormatCell(total, definition.Kind)
                            : first ? "Total" : "";
                        (IsNumber(definition) ? cell.AlignRight() : cell).Text(text).Bold();
                        first = false;
                    }
                }
            });

            if (section.Note is not null)
            {
                column.Item().PaddingTop(4).Text(section.Note).FontSize(7.5f).FontColor(Muted);
            }
        });
    }

    /// <summary>The first text column is the name that identifies a row, so it gets the most room.</summary>
    private static float Weight(ReportColumn column, IReadOnlyList<ReportColumn> all) => column.Kind switch
    {
        ReportColumnKind.Text when all.First(c => c.Kind == ReportColumnKind.Text) == column => 3f,
        ReportColumnKind.Text => 1.8f,
        ReportColumnKind.Date => 1.2f,
        ReportColumnKind.Money => 1.4f,
        ReportColumnKind.Quantity => 1.1f,
        _ => 0.8f
    };

    private static bool IsNumber(ReportColumn column) =>
        column.Kind is ReportColumnKind.Money or ReportColumnKind.Quantity or ReportColumnKind.Count;

    private static string FormatCell(object? value, ReportColumnKind kind) => value switch
    {
        null => "",
        decimal d => Format(d, kind),
        int i => Format(i, kind),
        DateOnly day => Day(day),
        _ => value.ToString() ?? ""
    };

    public static string Format(decimal value, ReportColumnKind kind) => kind switch
    {
        ReportColumnKind.Money => Money(value),
        ReportColumnKind.Quantity => Quantity(value),
        ReportColumnKind.Count => value.ToString("#,##0", India),
        _ => value.ToString(India)
    };
}
