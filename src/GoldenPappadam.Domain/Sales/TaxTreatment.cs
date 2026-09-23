namespace GoldenPappadam.Domain.Sales;

/// <summary>
/// How GST applies to a product. Which one is right for each pappadam is the accountant's call,
/// not the software's: the product carries it so nothing is ever assumed.
/// </summary>
public enum TaxTreatment
{
    /// <summary>GST is charged at the product's rate.</summary>
    Taxable,

    /// <summary>Taxable in principle but at a 0% rate. No tax is charged.</summary>
    NilRated,

    /// <summary>Exempt from GST by notification. No tax is charged.</summary>
    Exempt,

    /// <summary>Outside GST altogether. No tax is charged.</summary>
    NonGst
}
