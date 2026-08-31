namespace Treasury.App.Contracts.Tags;

public sealed class CreateTagRequest
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#3B82F6";
}
