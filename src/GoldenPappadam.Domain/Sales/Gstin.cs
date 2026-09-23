using System.Text.RegularExpressions;

namespace GoldenPappadam.Domain.Sales;

/// <summary>
/// A GSTIN is 15 characters: 2-digit state code, 10-character PAN, entity number, 'Z', checksum.
/// Only the shape is checked here; whether a number is actually registered is the GST portal's job.
/// </summary>
public static partial class Gstin
{
    [GeneratedRegex("^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$")]
    private static partial Regex Shape();

    public static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    public static bool IsValid(string value) => Shape().IsMatch(value);

    public static string StateCodeOf(string gstin) => gstin[..2];
}
