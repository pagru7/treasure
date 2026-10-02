namespace Treasury.App.Endpoints.Auth;

public sealed record AuthRequestPayload(string? Email, string? Password, string? ConfirmPassword, string? HouseholdNameOrId);
