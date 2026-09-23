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

    /// <summary>What a customer pays for a product, set or changed by the salesperson.</summary>
    CustomerPrice
}
