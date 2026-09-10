namespace Treasury.App.Contracts.Accounts;

public sealed class CreateAccountTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}