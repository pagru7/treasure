using FluentValidation;
using Treasury.App.Domain;

namespace Treasury.App.Contracts.Transactions;

public sealed class UpdateTransactionRequest
{
    public Guid Id { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public decimal? Amount { get; set; }
    public TransactionType? Type { get; set; }
    public DateTime? TransactionDate { get; set; }
    public List<Guid>? TagIds { get; set; }
}

public sealed class UpdateTransactionRequestValidator : AbstractValidator<UpdateTransactionRequest>
{
    public UpdateTransactionRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Transaction ID is required.");

        RuleFor(x => x)
            .Must(HaveEitherIdOrName)
            .WithMessage("You must specify either CategoryId or CategoryName, but not both.");
        RuleFor(x => x.Category)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Category))
            .WithMessage("CategoryName cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters.");

        RuleFor(x => x.Amount)
            .NotNull().WithMessage("Amount is required.")
            .GreaterThan(0).WithMessage("Amount must be greater than zero.");
        RuleFor(x => x.Type)
            .NotNull().WithMessage("Transaction type is required.");

        RuleFor(x => x.TransactionDate)
            .NotNull().WithMessage("Transaction date is required.")
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("Transaction date cannot be in the future.");
    }

    private bool HaveEitherIdOrName(UpdateTransactionRequest request)
    {
        bool hasId = request.CategoryId.HasValue;
        bool hasName = !string.IsNullOrWhiteSpace(request.Category);

        // Logical XOR: Exactly one must be true
        return hasId ^ hasName;
    }
}