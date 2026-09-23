using System.Globalization;
using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Sales;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GoldenPappadam.Api.Features.Sales.Invoices.Documents;

/// <summary>
/// Lays out an invoice as an A4 PDF: plain, printer-friendly and readable in black and white,
/// like a normal FMCG tax invoice. Every figure comes from the finalized invoice as saved - this
/// class does no arithmetic beyond adding up quantities - so the PDF cannot disagree with the
/// screen or the ledger.
///
/// The invoice data is the invoice's own snapshot. Only the business's contact lines, bank
/// details and terms come from the current settings, and those are frozen too once the PDF is
/// stored, because it is generated exactly once.
/// </summary>
public static class InvoicePdfRenderer
{
    private const string Ink = "#231F1A";
    private const string Muted = "#6B6258";
    private const string Rule = "#CFC6B8";
    private const string Accent = "#B8801F";
    private const string HeaderFill = "#F6EEDF";

    /// <summary>The Golden Pappadam mark, the same one the admin panel uses.</summary>
    private const string Logo = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
          <rect width="32" height="32" rx="7" fill="#231f1a"/>
          <circle cx="16" cy="16" r="9" fill="#e0a338"/>
          <circle cx="16" cy="16" r="9" fill="none" stroke="#f2c878" stroke-width="1.5"/>
          <circle cx="12.6" cy="13.6" r="1.1" fill="#a9741f"/>
          <circle cx="18.8" cy="15.2" r="0.9" fill="#a9741f"/>
          <circle cx="15.1" cy="19" r="1" fill="#a9741f"/>
        </svg>
        """;

    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

    static InvoicePdfRenderer()
    {
        // Free for businesses under USD 1M annual revenue; see questpdf.com/license.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public record BusinessDetails(string? Phone, string? Email, string? PaymentTerms, string? BankDetails, string? Terms);

    public static byte[] Render(InvoiceDetailDto invoice, BusinessDetails business) =>
        Compose(invoice, business).GeneratePdf();

    /// <summary>The document itself, for anything other than a PDF - page images in a preview or a test.</summary>
    public static IDocument Compose(InvoiceDetailDto invoice, BusinessDetails business) =>
        Document.Create(container => container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(style => style.FontSize(8.5f).FontColor(Ink));

                page.Header().Element(c => Header(c, invoice, business));
                page.Content().PaddingTop(10).Element(c => Content(c, invoice, business));
                page.Footer().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Text("This is a computer-generated invoice.").FontSize(7).FontColor(Muted);
                    row.RelativeItem().AlignRight().Text(text =>
                    {
                        text.DefaultTextStyle(s => s.FontSize(7).FontColor(Muted));
                        text.Span($"{invoice.InvoiceNumber} · Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });

                if (invoice.Status == InvoiceStatus.Cancelled)
                {
                    page.Foreground().AlignCenter().AlignMiddle().Rotate(-30)
                        .Text("CANCELLED").FontSize(72).Bold().FontColor("#D0453A40");
                }
            }))
            .WithMetadata(new DocumentMetadata
            {
                Title = $"{Title(invoice.DocumentType)} {invoice.InvoiceNumber}",
                Author = invoice.Supplier.Name,
                Creator = "Golden Pappadam",
                CreationDate = invoice.FinalizedAt,
                ModifiedDate = invoice.FinalizedAt
            });

    public static string Title(InvoiceDocumentType type) => type switch
    {
        InvoiceDocumentType.TaxInvoice => "Tax Invoice",
        InvoiceDocumentType.BillOfSupply => "Bill of Supply",
        _ => "Invoice"
    };

    private static void Header(IContainer container, InvoiceDetailDto invoice, BusinessDetails business)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(40).Height(40).Svg(Logo);

                row.RelativeItem().PaddingLeft(10).Column(supplier =>
                {
                    supplier.Item().Text(invoice.Supplier.Name.ToUpperInvariant()).FontSize(15).Bold().LetterSpacing(0.04f);

                    if (invoice.Supplier.Address is not null)
                    {
                        supplier.Item().Text(invoice.Supplier.Address).FontColor(Muted);
                    }

                    var contact = string.Join("  ·  ", new[]
                    {
                        business.Phone is null ? null : $"Phone {business.Phone}",
                        business.Email
                    }.Where(s => s is not null));

                    if (contact.Length > 0)
                    {
                        supplier.Item().Text(contact).FontColor(Muted);
                    }

                    supplier.Item().Text(text =>
                    {
                        if (invoice.Supplier.Gstin is not null)
                        {
                            text.Span("GSTIN ").FontColor(Muted);
                            text.Span(invoice.Supplier.Gstin).SemiBold();
                            text.Span("   ");
                        }

                        text.Span("State ").FontColor(Muted);
                        text.Span(IndianStates.Describe(invoice.Supplier.StateCode) ?? invoice.Supplier.StateCode);
                    });
                });

                row.ConstantItem(150).AlignRight().Column(title =>
                {
                    title.Item().AlignRight().Text(Title(invoice.DocumentType).ToUpperInvariant())
                        .FontSize(14).Bold().FontColor(Accent);
                    title.Item().AlignRight().Text("Original for recipient").FontSize(7.5f).FontColor(Muted);
                });
            });

            column.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor(Accent);
        });
    }

    private static void Content(IContainer container, InvoiceDetailDto invoice, BusinessDetails business)
    {
        var chargesTax = invoice.DocumentType == InvoiceDocumentType.TaxInvoice;

        container.Column(column =>
        {
            column.Spacing(10);

            column.Item().Row(row =>
            {
                row.Spacing(10);
                row.RelativeItem().Element(c => Party(c, "Bill to", invoice.Customer));

                if (invoice.Branch is not null)
                {
                    row.RelativeItem().Element(c => Party(c, "Deliver to (branch)", invoice.Branch));
                }

                row.RelativeItem().Element(c => Details(c, invoice, business));
            });

            column.Item().Element(c => Items(c, invoice, chargesTax));

            column.Item().Row(row =>
            {
                row.Spacing(14);

                row.RelativeItem().Column(left =>
                {
                    left.Spacing(8);

                    left.Item().Column(words =>
                    {
                        words.Item().Text("Amount in words").FontSize(7.5f).FontColor(Muted);
                        words.Item().Text(AmountInWords.Rupees(invoice.TotalAmount)).SemiBold();
                    });

                    if (chargesTax)
                    {
                        left.Item().Element(c => TaxSummary(c, invoice));
                    }

                    if (chargesTax && invoice.PricesIncludeTax)
                    {
                        left.Item().Text("Rates shown are inclusive of GST.").FontSize(7.5f).FontColor(Muted);
                    }
                });

                row.ConstantItem(200).Element(c => Totals(c, invoice, chargesTax));
            });

            column.Item().Row(row =>
            {
                row.Spacing(14);

                row.RelativeItem().Column(left =>
                {
                    left.Spacing(6);

                    if (business.BankDetails is not null)
                    {
                        left.Item().Element(c => Note(c, "Bank details", business.BankDetails));
                    }

                    if (business.Terms is not null)
                    {
                        left.Item().Element(c => Note(c, "Terms and conditions", business.Terms));
                    }

                    if (invoice.Notes is not null)
                    {
                        left.Item().Element(c => Note(c, "Notes", invoice.Notes));
                    }
                });

                row.ConstantItem(200).Border(0.75f).BorderColor(Rule).Padding(8).Column(sign =>
                {
                    sign.Item().AlignRight().Text($"For {invoice.Supplier.Name}").SemiBold();
                    sign.Item().Height(42);
                    sign.Item().AlignRight().Text("Authorised signatory").FontColor(Muted);
                });
            });
        });
    }

    private static void Party(IContainer container, string label, InvoicePartyDto party)
    {
        container.Border(0.75f).BorderColor(Rule).Padding(7).Column(column =>
        {
            column.Item().Text(label.ToUpperInvariant()).FontSize(7).FontColor(Muted).LetterSpacing(0.05f);
            column.Item().PaddingTop(2).Text(party.Name).FontSize(9.5f).Bold();

            if (party.Address is not null)
            {
                column.Item().Text(party.Address);
            }

            if (party.Phone is not null)
            {
                column.Item().Text($"Phone {party.Phone}").FontColor(Muted);
            }

            if (party.Gstin is not null)
            {
                column.Item().Text(text =>
                {
                    text.Span("GSTIN ").FontColor(Muted);
                    text.Span(party.Gstin).SemiBold();
                });
            }

            if (IndianStates.Describe(party.StateCode) is { } state)
            {
                column.Item().Text(text =>
                {
                    text.Span("State ").FontColor(Muted);
                    text.Span(state);
                });
            }
        });
    }

    private static void Details(IContainer container, InvoiceDetailDto invoice, BusinessDetails business)
    {
        container.Border(0.75f).BorderColor(Rule).Padding(7).Column(column =>
        {
            column.Spacing(1.5f);

            void Line(string label, string value, bool strong = false)
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(72).Text(label).FontColor(Muted);
                    var text = row.RelativeItem().Text(value);
                    if (strong) text.Bold();
                });
            }

            Line("Invoice no.", invoice.InvoiceNumber, strong: true);
            Line("Invoice date", invoice.InvoiceDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture));

            if (invoice.PlaceOfSupplyStateCode is not null)
            {
                Line("Place of supply", IndianStates.Describe(invoice.PlaceOfSupplyStateCode) ?? invoice.PlaceOfSupplyStateCode);
            }

            if (invoice.Supplier.Gstin is not null)
            {
                Line("Reverse charge", invoice.ReverseCharge ? "Yes" : "No");
            }

            if (business.PaymentTerms is not null)
            {
                Line("Payment terms", business.PaymentTerms);
            }
        });
    }

    private static void Items(IContainer container, InvoiceDetailDto invoice, bool chargesTax)
    {
        var showDiscount = invoice.Lines.Any(l => l.DiscountAmount != 0m);
        var showHsn = invoice.Lines.Any(l => l.HsnCode is not null);
        var showCess = invoice.CessAmount != 0m;
        var stateTaxLabel = IndianStates.Find(invoice.Supplier.StateCode)?.IsUnionTerritoryWithoutLegislature == true
            ? "UTGST"
            : "SGST";

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(18);
                columns.RelativeColumn(3.2f);
                if (showHsn) columns.ConstantColumn(50);
                columns.ConstantColumn(44);
                columns.ConstantColumn(56);
                if (showDiscount) columns.ConstantColumn(44);
                if (chargesTax)
                {
                    columns.ConstantColumn(58);
                    columns.ConstantColumn(28);
                    if (invoice.IsInterState)
                    {
                        columns.ConstantColumn(50);
                    }
                    else
                    {
                        columns.ConstantColumn(46);
                        columns.ConstantColumn(46);
                    }

                    if (showCess) columns.ConstantColumn(40);
                }

                columns.ConstantColumn(62);
            });

            table.Header(header =>
            {
                void Head(string text, bool right = true)
                {
                    var cell = header.Cell().Background(HeaderFill).BorderBottom(0.75f).BorderColor(Rule)
                        .PaddingVertical(4).PaddingHorizontal(3);
                    (right ? cell.AlignRight() : cell).Text(text).FontSize(7.5f).SemiBold();
                }

                Head("#", right: false);
                Head("Description", right: false);
                if (showHsn) Head("HSN", right: false);
                Head("Qty");
                Head("Rate");
                if (showDiscount) Head("Disc.");
                if (chargesTax)
                {
                    Head("Taxable");
                    Head("GST %");
                    if (invoice.IsInterState)
                    {
                        Head("IGST");
                    }
                    else
                    {
                        Head("CGST");
                        Head(stateTaxLabel);
                    }

                    if (showCess) Head("Cess");
                }

                Head("Amount");
            });

            for (var index = 0; index < invoice.Lines.Count; index++)
            {
                var line = invoice.Lines[index];

                IContainer Cell() => table.Cell().BorderBottom(0.5f).BorderColor(Rule).PaddingVertical(4).PaddingHorizontal(3);

                Cell().Text((index + 1).ToString(CultureInfo.InvariantCulture));
                Cell().Column(description =>
                {
                    description.Item().Text(line.Description).SemiBold();

                    // Say why a line carries no tax, so an exempt product never looks like a mistake.
                    if (line.TaxTreatment is { } treatment && treatment != TaxTreatment.Taxable)
                    {
                        description.Item().Text(Treatment(treatment)).FontSize(7).FontColor(Muted);
                    }
                });
                if (showHsn) Cell().Text(line.HsnCode ?? "");
                Cell().AlignRight().Text($"{Quantity(line.Quantity)} {line.UnitCode}");
                Cell().AlignRight().Text(Money(line.UnitPrice));
                if (showDiscount) Cell().AlignRight().Text(line.DiscountAmount == 0m ? "" : Money(line.DiscountAmount));
                if (chargesTax)
                {
                    Cell().AlignRight().Text(Money(line.TaxableValue));
                    Cell().AlignRight().Text(line.GstRate == 0m ? "—" : Rate(line.GstRate));
                    if (invoice.IsInterState)
                    {
                        Cell().AlignRight().Text(Money(line.IgstAmount));
                    }
                    else
                    {
                        Cell().AlignRight().Text(Money(line.CgstAmount));
                        Cell().AlignRight().Text(Money(line.SgstAmount));
                    }

                    if (showCess) Cell().AlignRight().Text(Money(line.CessAmount));
                }

                Cell().AlignRight().Text(Money(chargesTax ? line.Amount : line.LineTotal)).SemiBold();
            }
        });
    }

    private static void TaxSummary(IContainer container, InvoiceDetailDto invoice)
    {
        var groups = invoice.Lines
            .Where(l => l.TaxTreatment == TaxTreatment.Taxable)
            .GroupBy(l => (l.HsnCode, l.GstRate))
            .Select(g => new
            {
                g.Key.HsnCode,
                g.Key.GstRate,
                Taxable = g.Sum(l => l.TaxableValue),
                Cgst = g.Sum(l => l.CgstAmount),
                Sgst = g.Sum(l => l.SgstAmount),
                Igst = g.Sum(l => l.IgstAmount)
            })
            .ToList();

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn(1.3f);
                if (invoice.IsInterState)
                {
                    columns.RelativeColumn(1.3f);
                }
                else
                {
                    columns.RelativeColumn(1.3f);
                    columns.RelativeColumn(1.3f);
                }

                columns.RelativeColumn(1.3f);
            });

            table.Header(header =>
            {
                void Head(string text) =>
                    header.Cell().Background(HeaderFill).PaddingVertical(3).PaddingHorizontal(3).AlignRight()
                        .Text(text).FontSize(7).SemiBold();

                Head("HSN");
                Head("Taxable value");
                if (invoice.IsInterState)
                {
                    Head("IGST");
                }
                else
                {
                    Head("CGST");
                    Head("SGST");
                }

                Head("Total tax");
            });

            foreach (var group in groups)
            {
                IContainer Cell() => table.Cell().BorderBottom(0.5f).BorderColor(Rule).PaddingVertical(2.5f).PaddingHorizontal(3).AlignRight();

                Cell().Text(group.HsnCode ?? "—").FontSize(7.5f);
                Cell().Text(Money(group.Taxable)).FontSize(7.5f);
                if (invoice.IsInterState)
                {
                    Cell().Text($"{Money(group.Igst)} @ {Rate(group.GstRate)}").FontSize(7.5f);
                }
                else
                {
                    Cell().Text($"{Money(group.Cgst)} @ {Rate(group.GstRate / 2m)}").FontSize(7.5f);
                    Cell().Text($"{Money(group.Sgst)} @ {Rate(group.GstRate / 2m)}").FontSize(7.5f);
                }

                Cell().Text(Money(group.Cgst + group.Sgst + group.Igst)).FontSize(7.5f);
            }
        });
    }

    private static void Totals(IContainer container, InvoiceDetailDto invoice, bool chargesTax)
    {
        container.Border(0.75f).BorderColor(Rule).Padding(7).Column(column =>
        {
            column.Spacing(2);

            void Line(string label, string value)
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text(label).FontColor(Muted);
                    row.AutoItem().Text(value);
                });
            }

            Line("Total quantity", string.Join(", ", invoice.Lines
                .GroupBy(l => l.UnitCode)
                .Select(g => $"{Quantity(g.Sum(l => l.Quantity))} {g.Key}")));
            Line("Gross amount", Money(invoice.SubTotal));

            if (invoice.DiscountAmount != 0m)
            {
                Line("Discount", "- " + Money(invoice.DiscountAmount));
            }

            if (chargesTax)
            {
                Line("Taxable value", Money(invoice.TaxableAmount));

                if (invoice.IsInterState)
                {
                    Line("IGST", Money(invoice.IgstAmount));
                }
                else
                {
                    Line("CGST", Money(invoice.CgstAmount));
                    Line("SGST", Money(invoice.SgstAmount));
                }

                if (invoice.CessAmount != 0m)
                {
                    Line("Cess", Money(invoice.CessAmount));
                }
            }

            if (invoice.RoundOff != 0m)
            {
                Line("Round off", (invoice.RoundOff > 0m ? "+ " : "- ") + Money(Math.Abs(invoice.RoundOff)));
            }

            column.Item().PaddingTop(4).BorderTop(1).BorderColor(Ink).PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text("Grand total").Bold().FontSize(10);
                row.AutoItem().Text("Rs. " + Money(invoice.TotalAmount)).Bold().FontSize(10);
            });
        });
    }

    private static void Note(IContainer container, string label, string text) =>
        container.Column(column =>
        {
            column.Item().Text(label).FontSize(7.5f).FontColor(Muted);
            column.Item().Text(text).FontSize(7.5f);
        });

    private static string Treatment(TaxTreatment treatment) => treatment switch
    {
        TaxTreatment.Exempt => "Exempt",
        TaxTreatment.NilRated => "Nil rated",
        TaxTreatment.NonGst => "Non-GST supply",
        _ => ""
    };

    private static string Money(decimal value) => value.ToString("#,##0.00", India);

    private static string Quantity(decimal value) => value.ToString("#,##0.###", India);

    private static string Rate(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture) + "%";
}
