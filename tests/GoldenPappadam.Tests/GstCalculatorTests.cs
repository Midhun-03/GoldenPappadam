using GoldenPappadam.Api.Features.Sales.Invoices;
using GoldenPappadam.Api.Features.Sales.Invoices.Documents;
using GoldenPappadam.Domain.Sales;
using static GoldenPappadam.Api.Features.Sales.Invoices.GstCalculator;

namespace GoldenPappadam.Tests;

/// <summary>
/// The arithmetic on its own, no database. Every figure on a saved invoice, the preview and the
/// PDF comes out of this one calculation, so these are the tax rules.
/// </summary>
public class GstCalculatorTests
{
    private static readonly TaxBasis IntraExclusive = new(SupplierRegistered: true, PricesIncludeTax: false, IsInterState: false, RoundToNearestRupee: false);
    private static readonly TaxBasis InterExclusive = IntraExclusive with { IsInterState = true };
    private static readonly TaxBasis IntraInclusive = IntraExclusive with { PricesIncludeTax = true };
    private static readonly TaxBasis InterInclusive = InterExclusive with { PricesIncludeTax = true };

    private static LineInput Taxable(decimal quantity, decimal price, decimal rate) =>
        new(quantity, price, TaxTreatment.Taxable, rate);

    [Fact]
    public void Same_state_charges_cgst_and_sgst_as_equal_halves()
    {
        var result = Calculate([Taxable(10m, 100m, 5m)], 0m, IntraExclusive);

        Assert.Equal(1000m, result.TaxableAmount);
        Assert.Equal(25m, result.CgstAmount);
        Assert.Equal(25m, result.SgstAmount);
        Assert.Equal(0m, result.IgstAmount);
        Assert.Equal(1050m, result.TotalAmount);
    }

    [Fact]
    public void Another_state_charges_igst_and_never_cgst_or_sgst()
    {
        var result = Calculate([Taxable(10m, 100m, 18m)], 0m, InterExclusive);

        Assert.Equal(180m, result.IgstAmount);
        Assert.Equal(0m, result.CgstAmount);
        Assert.Equal(0m, result.SgstAmount);
        Assert.Equal(1180m, result.TotalAmount);
    }

    [Fact]
    public void A_tax_inclusive_rate_is_split_so_the_total_stays_exactly_what_was_agreed()
    {
        // Danya pays Rs 37 a packet, GST included. The customer must still owe exactly 37.
        var result = Calculate([Taxable(1m, 37m, 5m)], 0m, IntraInclusive);

        Assert.Equal(0.88m, result.CgstAmount);
        Assert.Equal(0.88m, result.SgstAmount);
        Assert.Equal(35.24m, result.TaxableAmount);
        Assert.Equal(37m, result.TotalAmount);
    }

    [Fact]
    public void A_tax_inclusive_rate_across_states_is_split_into_igst()
    {
        var result = Calculate([Taxable(10m, 112m, 12m)], 0m, InterInclusive);

        Assert.Equal(1000m, result.TaxableAmount);
        Assert.Equal(120m, result.IgstAmount);
        Assert.Equal(1120m, result.TotalAmount);
    }

    [Theory]
    [InlineData(TaxTreatment.Exempt)]
    [InlineData(TaxTreatment.NilRated)]
    [InlineData(TaxTreatment.NonGst)]
    public void Goods_gst_does_not_charge_carry_no_tax_whatever_the_rate_says(TaxTreatment treatment)
    {
        var result = Calculate([new LineInput(10m, 100m, treatment, null)], 0m, IntraExclusive);

        var line = Assert.Single(result.Lines);
        Assert.Equal(treatment, line.Treatment);
        Assert.Equal(0m, line.GstRate);
        Assert.Equal(1000m, line.TaxableValue);
        Assert.False(result.ChargesTax);
        Assert.Equal(1000m, result.TotalAmount);
    }

    [Fact]
    public void Without_a_gstin_no_tax_is_charged_even_on_a_taxable_product()
    {
        var result = Calculate([Taxable(10m, 100m, 5m)], 0m, IntraExclusive with { SupplierRegistered = false });

        var line = Assert.Single(result.Lines);
        Assert.Null(line.Treatment);
        Assert.False(result.ChargesTax);
        Assert.Equal(1000m, result.TotalAmount);
    }

    [Fact]
    public void Each_line_is_taxed_at_its_own_rate_and_the_totals_are_the_sums_of_the_lines()
    {
        var result = Calculate(
            [
                Taxable(10m, 100m, 5m),
                Taxable(2m, 250m, 18m),
                new LineInput(5m, 40m, TaxTreatment.Exempt, null)
            ],
            0m,
            IntraExclusive);

        Assert.Equal([25m, 45m, 0m], result.Lines.Select(l => l.CgstAmount));
        Assert.Equal(1700m, result.TaxableAmount);
        Assert.Equal(70m, result.CgstAmount);
        Assert.Equal(70m, result.SgstAmount);
        Assert.Equal(1840m, result.TotalAmount);
        Assert.Equal(result.TotalAmount, result.Lines.Sum(l => l.Amount));
    }

    [Fact]
    public void A_bill_discount_lowers_the_taxable_value_before_tax_is_worked_out()
    {
        var result = Calculate([Taxable(10m, 100m, 5m)], 100m, IntraExclusive);

        Assert.Equal(900m, result.TaxableAmount);
        Assert.Equal(22.5m, result.CgstAmount);
        Assert.Equal(945m, result.TotalAmount);
    }

    [Fact]
    public void A_discount_shared_across_lines_adds_back_up_to_the_paisa()
    {
        var result = Calculate(
            [Taxable(1m, 100m, 5m), Taxable(1m, 100m, 5m), Taxable(1m, 100m, 5m)],
            10m,
            IntraExclusive);

        Assert.Equal(10m, result.Lines.Sum(l => l.DiscountAmount));
        Assert.All(result.Lines, line => Assert.InRange(line.DiscountAmount, 3.33m, 3.34m));
    }

    [Fact]
    public void Rounding_to_the_rupee_is_shown_as_a_round_off_and_is_off_unless_asked_for()
    {
        // 3 × 33.47 = 100.41 at 5% exclusive: 2.51 + 2.51 tax, 105.43 before rounding.
        var line = Taxable(3m, 33.47m, 5m);

        var exact = Calculate([line], 0m, IntraExclusive);
        var rounded = Calculate([line], 0m, IntraExclusive with { RoundToNearestRupee = true });

        Assert.Equal(0m, exact.RoundOff);
        Assert.Equal(105.43m, exact.TotalAmount);
        Assert.Equal(-0.43m, rounded.RoundOff);
        Assert.Equal(105m, rounded.TotalAmount);
    }

    [Theory]
    [InlineData(0, "Rupees Zero Only")]
    [InlineData(100, "Rupees One Hundred Only")]
    [InlineData(45.5, "Rupees Forty-Five and Fifty Paise Only")]
    [InlineData(123456.70, "Rupees One Lakh Twenty-Three Thousand Four Hundred Fifty-Six and Seventy Paise Only")]
    [InlineData(123456789, "Rupees Twelve Crore Thirty-Four Lakh Fifty-Six Thousand Seven Hundred Eighty-Nine Only")]
    public void Amounts_are_written_the_indian_way(decimal amount, string expected) =>
        Assert.Equal(expected, AmountInWords.Rupees(amount));

    [Theory]
    [InlineData(2026, 3, 31, "2025-26")]
    [InlineData(2026, 4, 1, "2026-27")]
    [InlineData(2099, 12, 31, "2099-00")]
    public void The_financial_year_turns_on_the_first_of_april(int year, int month, int day, string expected) =>
        Assert.Equal(expected, InvoiceNumbering.FinancialYearOf(new DateOnly(year, month, day)));

    [Fact]
    public void An_invoice_number_fits_inside_the_sixteen_characters_gst_allows()
    {
        var number = InvoiceNumbering.Format("GPA", "2026-27", 999_999);

        Assert.Equal("GPA/26-27/999999", number);
        Assert.True(number.Length <= 16);
    }
}
