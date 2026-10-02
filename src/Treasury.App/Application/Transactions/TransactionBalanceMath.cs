using Treasury.App.Domain;

namespace Treasury.App.Application.Transactions;

public static class TransactionBalanceMath
{
    public static decimal GetDelta(decimal amount, TransactionType type)
    {
        return type switch
        {
            TransactionType.Expense => -Math.Abs(amount),
            TransactionType.Income => Math.Abs(amount),
            TransactionType.Transfer => Math.Abs(amount),
            TransactionType.TransferIn => Math.Abs(amount),
            TransactionType.TransferOut => -Math.Abs(amount),
            _ => amount
        };
    }
}