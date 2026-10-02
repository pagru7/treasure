using System.Text.Json;

namespace Treasury.App.Endpoints.Auth;

public static class AuthRequestReader
{
    public static async Task<AuthRequestPayload> ReadAsync(HttpRequest request)
    {
        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync();
            return new AuthRequestPayload(
                form["Email"].ToString(),
                form["Password"].ToString(),
                form["ConfirmPassword"].ToString(),
                form["HouseholdNameOrId"].ToString());
        }

        if (request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            var raw = await new StreamReader(request.Body, leaveOpen: true).ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(raw))
            {
                return new AuthRequestPayload(string.Empty, string.Empty, string.Empty, string.Empty);
            }

            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;

            return new AuthRequestPayload(
                GetStringProperty(root, "Email"),
                GetStringProperty(root, "Password"),
                GetStringProperty(root, "ConfirmPassword"),
                GetStringProperty(root, "HouseholdNameOrId"));
        }

        return new AuthRequestPayload(string.Empty, string.Empty, string.Empty, string.Empty);
    }

    private static string? GetStringProperty(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!root.TryGetProperty(propertyName, out var property))
        {
            foreach (var candidate in root.EnumerateObject())
            {
                if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    property = candidate.Value;
                    break;
                }
            }

            if (property.ValueKind == JsonValueKind.Undefined)
            {
                return null;
            }
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }
}
