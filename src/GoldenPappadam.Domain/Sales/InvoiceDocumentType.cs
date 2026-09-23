namespace GoldenPappadam.Domain.Sales;

/// <summary>What the printed document is called, which GST law decides rather than taste.</summary>
public enum InvoiceDocumentType
{
    /// <summary>The business has no GSTIN on file, so no tax is charged or shown.</summary>
    Invoice,

    /// <summary>A registered supplier charging GST on at least one line.</summary>
    TaxInvoice,

    /// <summary>A registered supplier whose bill carries only exempt, nil-rated or non-GST goods.</summary>
    BillOfSupply
}
