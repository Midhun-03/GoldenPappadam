namespace GoldenPappadam.Domain.Sales;

/// <summary>Why a packet came back. Good packets are never returned, so these are the only two.</summary>
public enum ReturnReason
{
    /// <summary>Past its shelf life.</summary>
    Expired,

    /// <summary>Broken, wet, torn - unusable before its time.</summary>
    Damaged
}
