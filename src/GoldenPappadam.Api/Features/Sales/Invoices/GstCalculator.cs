using GoldenPappadam.Domain.Sales;

namespace GoldenPappadam.Api.Features.Sales.Invoices;

/// <summary>
/// Every amount on an invoice, worked out in one place so the preview on screen, the saved
/// invoice and the PDF can never disagree. Pure arithmetic - no database - so each rule is tested
/// on its own.
///
/// Rounding: every amount is rounded to the paisa, half away from zero, at the line; invoice
/// totals are the sums of the rounded lines. CGST and SGST are each half the rate on the same
/// taxable value, so they are always equal. The grand total is rounded to the rupee only when
/// the business asks for it, and the difference is shown as the round-off.
/// </summary>
public static class GstCalculator
{
    public record LineInput(decimal Quantity, decimal UnitPrice, TaxTreatment? Treatment, decimal? GstRate);

    public record TaxBasis(bool SupplierRegistered, bool PricesIncludeTax, bool IsInterState, bool RoundToNearestRupee);

    public record LineResult(
        decimal LineTotal,
        decimal DiscountAmount,
        TaxTreatment? Treatment,
        decimal GstRate,
        decimal TaxableValue,
        decimal CgstAmount,
        decimal SgstAmount,
        decimal IgstAmount,
        decimal CessAmount)
    {
        /// <summary>What this line adds to the grand total, before any round-off.</summary>
        public decimal Amount => TaxableValue + CgstAmount + SgstAmount + IgstAmount + CessAmount;
    }

    public record Result(
        IReadOnlyList<LineResult> Lines,
        decimal SubTotal,
        decimal DiscountAmount,
        decimal TaxableAmount,
        decimal CgstAmount,
        decimal SgstAmount,
        decimal IgstAmount,
        decimal CessAmount,
        decimal RoundOff,
        decimal TotalAmount)
    {
        public bool ChargesTax => CgstAmount + SgstAmount + IgstAmount + CessAmount > 0m;
    }

    public static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    public static Result Calculate(IReadOnlyList<LineInput> lines, decimal discount, TaxBasis basis)
    {
        var gross = lines.Select(l => Round(l.Quantity * l.UnitPrice)).ToList();
        var subTotal = gross.Sum();
        var shares = ShareDiscount(gross, discount);

        var results = lines
            .Select((line, index) => CalculateLine(line, gross[index], shares[index], basis))
            .ToList();

        var taxable = results.Sum(r => r.TaxableValue);
        var cgst = results.Sum(r => r.CgstAmount);
        var sgst = results.Sum(r => r.SgstAmount);
        var igst = results.Sum(r => r.IgstAmount);
        var cess = results.Sum(r => r.CessAmount);
        var beforeRounding = taxable + cgst + sgst + igst + cess;

        var roundOff = basis.RoundToNearestRupee
            ? decimal.Round(beforeRounding, 0, MidpointRounding.AwayFromZero) - beforeRounding
            : 0m;

        return new Result(results, subTotal, discount, taxable, cgst, sgst, igst, cess, roundOff, beforeRounding + roundOff);
    }

    private static LineResult CalculateLine(LineInput line, decimal gross, decimal discount, TaxBasis basis)
    {
        var net = gross - discount;

        // No GSTIN, or goods GST does not charge: the whole amount is the value, with no tax in it.
        if (!basis.SupplierRegistered || line.Treatment != TaxTreatment.Taxable || line.GstRate is not > 0m)
        {
            return new LineResult(
                gross, discount, basis.SupplierRegistered ? line.Treatment : null, 0m, net, 0m, 0m, 0m, 0m);
        }

        var rate = line.GstRate.Value;

        if (basis.IsInterState)
        {
            var (taxable, igst) = basis.PricesIncludeTax
                ? (net - Round(net * rate / (100m + rate)), Round(net * rate / (100m + rate)))
                : (net, Round(net * rate / 100m));

            return new LineResult(gross, discount, TaxTreatment.Taxable, rate, taxable, 0m, 0m, igst, 0m);
        }

        // Each half is worked out once and used twice, so CGST always equals SGST to the paisa.
        var half = basis.PricesIncludeTax
            ? Round(net * (rate / 2m) / (100m + rate))
            : Round(net * (rate / 2m) / 100m);
        var taxableValue = basis.PricesIncludeTax ? net - 2m * half : net;

        return new LineResult(gross, discount, TaxTreatment.Taxable, rate, taxableValue, half, half, 0m, 0m);
    }

    /// <summary>
    /// A bill-level discount lowers the taxable value of the goods, so it is spread over the lines
    /// in proportion to their amounts. Rounding leftovers land on the largest line, so the shares
    /// add up to the discount exactly.
    /// </summary>
    private static decimal[] ShareDiscount(IReadOnlyList<decimal> gross, decimal discount)
    {
        var shares = new decimal[gross.Count];
        var total = gross.Sum();

        if (discount == 0m || total == 0m)
        {
            return shares;
        }

        for (var i = 0; i < gross.Count; i++)
        {
            shares[i] = Round(discount * gross[i] / total);
        }

        var largest = gross.Select((amount, index) => (amount, index)).MaxBy(x => x.amount).index;
        shares[largest] += discount - shares.Sum();

        return shares;
    }
}
