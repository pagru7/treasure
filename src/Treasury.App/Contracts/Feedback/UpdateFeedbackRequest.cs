namespace Treasury.App.Contracts.Feedback;

public sealed class UpdateFeedbackRequest
{
    public Guid Id { get; set; }
    public string Status { get; set; } = "new";
    public string Description { get; set; } = string.Empty;
}
