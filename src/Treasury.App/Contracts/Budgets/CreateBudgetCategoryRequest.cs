namespace Treasury.App.Contracts.Budgets;

public sealed class CreateBudgetCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal MonthlyLimit { get; set; }
    public string Currency { get; set; } = "PLN";
    public string Notes { get; set; } = string.Empty;
}
