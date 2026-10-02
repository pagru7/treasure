namespace Treasury.App.Contracts.Feedback;

public sealed class CreateFeedbackRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}