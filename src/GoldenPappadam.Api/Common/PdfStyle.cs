using System.Globalization;
using QuestPDF.Infrastructure;

namespace GoldenPappadam.Api.Common;

/// <summary>
/// The Golden Pappadam house style for every printed document - invoices, reports, statements -
/// so they look like they come from the same business and format numbers the same way.
/// </summary>
public static class PdfStyle
{
    public const string Ink = "#231F1A";
    public const string Muted = "#6B6258";
    public const string Rule = "#CFC6B8";
    public const string Accent = "#B8801F";
    public const string HeaderFill = "#F6EEDF";

    /// <summary>The Golden Pappadam mark, the same one the admin panel uses.</summary>
    public const string Logo = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
          <rect width="32" height="32" rx="7" fill="#231f1a"/>
          <circle cx="16" cy="16" r="9" fill="#e0a338"/>
          <circle cx="16" cy="16" r="9" fill="none" stroke="#f2c878" stroke-width="1.5"/>
          <circle cx="12.6" cy="13.6" r="1.1" fill="#a9741f"/>
          <circle cx="18.8" cy="15.2" r="0.9" fill="#a9741f"/>
          <circle cx="15.1" cy="19" r="1" fill="#a9741f"/>
        </svg>
        """;

    public static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

    /// <summary>
    /// QuestPDF's Community licence: free for businesses under USD 1M annual revenue
    /// (questpdf.com/license). Called by every renderer, so tests that render without the app
    /// running are licensed too.
    /// </summary>
    public static void UseCommunityLicense() => QuestPDF.Settings.License = LicenseType.Community;

    /// <summary>1,23,456.00 - Indian digit grouping.</summary>
    public static string Money(decimal value) => value.ToString("#,##0.00", India);

    public static string Quantity(decimal value) => value.ToString("#,##0.###", India);

    public static string Day(DateOnly value) => value.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
}
