namespace GoldenPappadam.Domain.Sales;

public enum PaymentMethod
{
    Cash,
    UPI,
    BankTransfer,
    Cheque,
    Other,

    /// <summary>
    /// Not money: a credit given for returned packets (see <see cref="ReturnNote"/>). It reduces what
    /// the shop owes like any payment, but is never counted as cash collected, and can only be
    /// created by a return - never typed in as a payment.
    /// </summary>
    ReturnCredit
}
