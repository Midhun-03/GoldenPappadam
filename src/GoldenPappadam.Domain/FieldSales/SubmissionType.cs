namespace GoldenPappadam.Domain.FieldSales;

public enum SubmissionType
{
    Invoice,
    Payment,
    Visit,

    /// <summary>Stock the salesperson loaded onto their own van.</summary>
    VanLoad,

    /// <summary>What the salesperson needs the packing unit to pack.</summary>
    StockRequest
}
