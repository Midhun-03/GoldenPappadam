namespace GoldenPappadam.Api.Features.Sales.Invoices.Documents;

/// <summary>
/// "Rupees One Lakh Twenty-Three Thousand Four Hundred Fifty-Six and Seventy Paise Only" - the
/// Indian numbering system (thousand, lakh, crore), the way an invoice in India states its total.
/// </summary>
public static class AmountInWords
{
    private static readonly string[] Ones =
    [
        "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
        "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"
    ];

    private static readonly string[] Tens =
        ["", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"];

    public static string Rupees(decimal amount)
    {
        if (amount < 0m)
        {
            return "Minus " + Rupees(-amount);
        }

        var rounded = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        var rupees = (long)decimal.Truncate(rounded);
        var paise = (int)((rounded - rupees) * 100m);

        var words = $"Rupees {Words(rupees)}";

        if (paise > 0)
        {
            words += $" and {Words(paise)} Paise";
        }

        return words + " Only";
    }

    private static string Words(long number)
    {
        if (number < 20)
        {
            return Ones[number];
        }

        if (number < 100)
        {
            return Tens[number / 10] + (number % 10 > 0 ? "-" + Ones[number % 10] : "");
        }

        var parts = new List<string>();

        void Take(long unit, string name)
        {
            if (number >= unit)
            {
                parts.Add($"{Words(number / unit)} {name}");
                number %= unit;
            }
        }

        Take(10_000_000, "Crore");
        Take(100_000, "Lakh");
        Take(1_000, "Thousand");
        Take(100, "Hundred");

        if (number > 0)
        {
            parts.Add(Words(number));
        }

        return string.Join(" ", parts);
    }
}
