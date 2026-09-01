namespace Treasury.App.Domain;

public sealed class VisibilityRule
{
    public Guid AccountId { get; set; }
    public string ViewerUserId { get; set; } = string.Empty;
    public bool IsReadOnly { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Account Account { get; set; } = default!;
}
