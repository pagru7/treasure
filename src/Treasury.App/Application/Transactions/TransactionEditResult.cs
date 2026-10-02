namespace Treasury.App.Application.Transactions;

public sealed record TransactionEditResult(
    TransactionEditStatus Status,
    string? Message,
    IReadOnlyList<TransactionEditIssue> Issues,
    TransactionEditOutcome? Outcome)
{
    public bool Succeeded => Status == TransactionEditStatus.Success && Outcome is not null;

    public static TransactionEditResult Success(TransactionEditOutcome outcome) =>
        new(TransactionEditStatus.Success, null, [], outcome);

    public static TransactionEditResult Failure(
        TransactionEditStatus status,
        string message,
        params TransactionEditIssue[] issues) =>
        new(status, message, issues, null);
}