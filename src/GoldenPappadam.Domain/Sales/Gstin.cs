using System.Globalization;

namespace GoldenPappadam.Domain.Sales;

/// <summary>
/// A GSTIN is 15 characters: 2-digit state code, 10-character PAN, entity number, 'Z', checksum.
/// Only the shape is checked here; whether a number is actually registered is the GST portal's job.
/// </summary>
public static class Gstin
{
    /// <summary>
    /// Upper case, with every space and invisible character removed - a GSTIN copied from WhatsApp or
    /// a PDF often carries a non-breaking or zero-width space that makes a correct number look wrong.
    /// </summary>
    public static string? Normalise(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var cleaned = new string(value
            .Where(c => !char.IsWhiteSpace(c) &&
                        CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.Format &&
                        c != '-')
            .ToArray())
            .ToUpperInvariant();

        return cleaned.Length == 0 ? null : cleaned;
    }

    /// <summary>
    /// Null when the GSTIN has the right shape; otherwise which character is wrong, in words, so a
    /// typo such as the letter O among the PAN's digits can be found without guessing.
    /// </summary>
    public static string? Problem(string value)
    {
        if (value.Length != 15)
        {
            return $"it has {value.Length} characters, and a GSTIN has 15.";
        }

        static bool Digits(string text) => text.All(char.IsAsciiDigit);
        static bool Letters(string text) => text.All(char.IsAsciiLetterUpper);

        if (!Digits(value[..2]))
        {
            return "the first 2 characters must be the state code in digits, like 32 for Kerala.";
        }

        if (!Letters(value[2..7]))
        {
            return $"characters 3 to 7 ('{value[2..7]}') must be letters - the start of the PAN.";
        }

        if (!Digits(value[7..11]))
        {
            return $"characters 8 to 11 ('{value[7..11]}') must be digits - check for the letter O instead of zero.";
        }

        if (!char.IsAsciiLetterUpper(value[11]))
        {
            return $"character 12 ('{value[11]}') must be a letter - the end of the PAN.";
        }

        if (!(value[12] is >= '1' and <= '9' || char.IsAsciiLetterUpper(value[12])))
        {
            return $"character 13 ('{value[12]}') must be 1 to 9 or a letter.";
        }

        if (value[13] != 'Z')
        {
            return $"character 14 ('{value[13]}') is always Z.";
        }

        if (!char.IsAsciiLetterOrDigit(value[14]))
        {
            return $"character 15 ('{value[14]}') must be a letter or a digit.";
        }

        return null;
    }

    public static string StateCodeOf(string gstin) => gstin[..2];
}
