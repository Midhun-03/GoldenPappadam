using ClosedXML.Excel;

namespace GoldenPappadam.Api.Features.Reports;

/// <summary>
/// Writes any <see cref="ReportDocument"/> as an .xlsx sheet the accountant can work with: real
/// numbers and dates (not text that looks like them), money in Indian grouping, and each table's
/// totals row. The totals are the report's own figures, so the sheet matches the screen and the PDF.
/// </summary>
public static class ReportExcelWriter
{
    // Plain thousands grouping: Excel number formats cannot express lakh grouping portably, and the
    // values stay real numbers either way, so sums and formulas work.
    private const string MoneyFormat = "#,##0.00";
    private const string QuantityFormat = "General";
    private const string CountFormat = "#,##0";
    private const string DateFormat = "dd-mm-yyyy";

    public static byte[] Write(ReportDocument report, string businessName)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SheetName(report.Title));
        var row = 1;

        sheet.Cell(row, 1).Value = $"{businessName} — {report.Title}";
        sheet.Cell(row, 1).Style.Font.SetBold().Font.SetFontSize(13);
        row++;
        sheet.Cell(row, 1).Value = report.Period;
        row++;

        if (report.Subtitle is not null)
        {
            sheet.Cell(row, 1).Value = report.Subtitle;
            row++;
        }

        if (report.Summary.Count > 0)
        {
            row++;

            foreach (var figure in report.Summary)
            {
                sheet.Cell(row, 1).Value = figure.Label;
                Set(sheet.Cell(row, 2), figure.Value, figure.Kind);
                sheet.Cell(row, 2).Style.Font.SetBold();
                row++;
            }
        }

        foreach (var section in report.Sections)
        {
            row++;
            sheet.Cell(row, 1).Value = section.Title;
            sheet.Cell(row, 1).Style.Font.SetBold().Font.SetFontSize(12);
            row++;

            for (var c = 0; c < section.Columns.Count; c++)
            {
                var header = sheet.Cell(row, c + 1);
                header.Value = section.Columns[c].Title;
                header.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#F6EEDF"));
                if (IsNumber(section.Columns[c])) header.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            }

            row++;

            foreach (var values in section.Rows)
            {
                for (var c = 0; c < section.Columns.Count; c++)
                {
                    var column = section.Columns[c];
                    Set(sheet.Cell(row, c + 1), values.TryGetValue(column.Key, out var v) ? v : null, column.Kind);
                }

                row++;
            }

            if (section.Totals is not null)
            {
                sheet.Cell(row, 1).Value = "Total";

                for (var c = 0; c < section.Columns.Count; c++)
                {
                    var column = section.Columns[c];
                    if (section.Totals.TryGetValue(column.Key, out var total))
                    {
                        Set(sheet.Cell(row, c + 1), total, column.Kind);
                    }
                }

                sheet.Row(row).Style.Font.SetBold();
                sheet.Row(row).Style.Border.SetTopBorder(XLBorderStyleValues.Thin);
                row++;
            }

            if (section.Note is not null)
            {
                sheet.Cell(row, 1).Value = section.Note;
                sheet.Cell(row, 1).Style.Font.SetItalic();
                row++;
            }
        }

        sheet.Columns().AdjustToContents(1, row, 8, 60);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return stream.ToArray();
    }

    private static void Set(IXLCell cell, object? value, ReportColumnKind kind)
    {
        switch (value)
        {
            case null:
                return;
            case decimal d:
                cell.Value = d;
                break;
            case int i:
                cell.Value = i;
                break;
            case DateOnly day:
                cell.Value = day.ToDateTime(TimeOnly.MinValue);
                break;
            default:
                cell.Value = value.ToString();
                return;
        }

        cell.Style.NumberFormat.Format = kind switch
        {
            ReportColumnKind.Money => MoneyFormat,
            ReportColumnKind.Quantity => QuantityFormat,
            ReportColumnKind.Count => CountFormat,
            ReportColumnKind.Date => DateFormat,
            _ => cell.Style.NumberFormat.Format
        };
    }

    private static bool IsNumber(ReportColumn column) =>
        column.Kind is ReportColumnKind.Money or ReportColumnKind.Quantity or ReportColumnKind.Count;

    /// <summary>Excel sheet names are at most 31 characters and cannot contain some punctuation.</summary>
    private static string SheetName(string title)
    {
        var cleaned = new string(title.Where(c => !"[]:*?/\\".Contains(c)).ToArray());
        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }
}
