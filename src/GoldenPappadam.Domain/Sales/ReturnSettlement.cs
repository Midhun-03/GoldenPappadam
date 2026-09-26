namespace GoldenPappadam.Domain.Sales;

/// <summary>What a shop got for the packets it gave back - the office decides, shop by shop.</summary>
public enum ReturnSettlement
{
    /// <summary>Recorded, not yet decided. The office settles it later (a pickup from the phone, say).</summary>
    Pending,

    /// <summary>Fresh packets given in their place, free. Stock goes out; money does not change.</summary>
    Replacement,

    /// <summary>The shop's account is reduced by the value of what came back.</summary>
    Credit,

    /// <summary>Taken back and recorded, nothing given. The shop owes the same.</summary>
    NoCompensation
}
