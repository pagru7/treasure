using Treasury.App.Domain;

namespace Treasury.App.Application.Transactions;

public sealed record TransactionEditOutcome(
    Guid Id,
    Guid AccountId,
    Guid CategoryId,
    string Description,
    string Category,
    decimal Amount,
    string Currency,
    TransactionType Type,
    DateTime TransactionDate,
    decimal BalanceAfterTransaction,
    IReadOnlyList<Guid> TagIds);
