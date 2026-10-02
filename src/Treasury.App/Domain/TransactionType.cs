namespace Treasury.App.Domain;

public enum TransactionType
{
    Expense = 0,
    Income = 1,
    Transfer = 2,
    TransferIn = 3,
    TransferOut = 4,
    BalanceCorrection = 5
}
