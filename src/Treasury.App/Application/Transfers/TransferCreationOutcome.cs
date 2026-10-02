namespace Treasury.App.Application.Transfers;

public sealed record TransferCreationOutcome(
    Guid TransferId,
    Guid OutflowTransactionId,
    Guid InflowTransactionId,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    string Currency,
    DateTime TransferDate);
