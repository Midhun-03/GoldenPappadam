namespace GoldenPappadam.Domain.FieldSales;

public enum SubmissionType
{
    Invoice,
    Payment,
    Visit,

    /// <summary>Stock the salesperson loaded onto their own van.</summary>
    VanLoad,

    /// <summary>What the salesperson needs the packing unit to pack.</summary>
    StockRequest,

    /// <summary>A shop the salesperson found, or new details for one. The id is the phone's.</summary>
    Customer,

    /// <summary>A branch under a customer, new or edited. The id is the phone's.</summary>
    CustomerBranch,

    /// <summary>
    /// What a customer pays for a product. From 2026-09-30 a phone cannot change a rate: this is kept for
    /// older app versions and is recorded as a rate-change request.
    /// </summary>
    CustomerPrice,

    /// <summary>Expired or damaged packets collected from a shop, and any fresh ones handed over.</summary>
    Return,

    /// <summary>A request for the office to change a customer's rate. The id is the phone's.</summary>
    RateRequest,

    /// <summary>The salesperson withdrawing their own pending rate-change request.</summary>
    RateRequestCancel
}
