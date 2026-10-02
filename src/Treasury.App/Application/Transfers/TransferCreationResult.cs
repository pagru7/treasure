namespace Treasury.App.Application.Transfers;

public sealed record TransferCreationResult(
    TransferCreationStatus Status,
    string? Message,
    IReadOnlyList<TransferCreationIssue> Issues,
    TransferCreationOutcome? Outcome)
{
    public bool Succeeded => Status == TransferCreationStatus.Success && Outcome is not null;

    public static TransferCreationResult Success(TransferCreationOutcome outcome) =>
        new(TransferCreationStatus.Success, null, [], outcome);

    public static TransferCreationResult Failure(
        TransferCreationStatus status,
        string message,
        params TransferCreationIssue[] issues) =>
        new(status, message, issues, null);
}
