namespace Treasury.App.Contracts.Bills;

public sealed class CreateBillRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "PLN";
    public int DueDay { get; set; } = 1;
    public bool IsPaid { get; set; }
    public string Notes { get; set; } = string.Empty;
}
