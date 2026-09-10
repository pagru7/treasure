namespace Treasury.App.Application.Accounts;

public static class AccountLifecycleValidation
{
    public static bool TryNormalizeOptionalBankAccountNumber(string? bankAccountNumber, out string? normalized, out string? errorMessage)
    {
        normalized = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(bankAccountNumber))
        {
            return true;
        }

        normalized = bankAccountNumber.Trim();
        if (normalized.Length is < 16 or > 34 || normalized.Any(ch => !char.IsDigit(ch)))
        {
            errorMessage = "Bank account number must contain only digits and be 16 to 34 characters long.";
            normalized = null;
            return false;
        }

        return true;
    }
}