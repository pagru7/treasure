using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Treasury.App.Application.Transactions;
using Treasury.App.Contracts.Transactions;
using Treasury.App.Domain;

namespace Treasury.App.Endpoints.Transactions;

public sealed class UpdateTransactionEndpoint(
    TransactionEditingService transactionEditingService,
    UserManager<ApplicationUser> userManager)
    : Endpoint<UpdateTransactionRequest>
{
    public override void Configure()
    {
        Put("/api/transactions/{id:guid}");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.SharedReadOnly);
    }

    public override async Task HandleAsync(UpdateTransactionRequest request, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await transactionEditingService.UpdateAsync(user, request, ct);
        if (!result.Succeeded)
        {
            if (result.Status == TransactionEditStatus.NotFound)
            {
                await SendNotFoundAsync(ct);
                return;
            }

            if (result.Status == TransactionEditStatus.Forbidden)
            {
                await SendForbiddenAsync(ct);
                return;
            }

            if (result.Issues.Count > 0)
            {
                foreach (var issue in result.Issues)
                {
                    AddError(issue.Message);
                }
            }
            else if (!string.IsNullOrWhiteSpace(result.Message))
            {
                AddError(result.Message);
            }

            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var outcome = result.Outcome!;
        await SendAsync(new
        {
            outcome.Id,
            outcome.AccountId,
            outcome.Description,
            outcome.Category,
            outcome.Amount,
            outcome.Currency,
            outcome.Type,
            outcome.TransactionDate,
            outcome.BalanceAfterTransaction,
            Tags = outcome.TagIds
        }, StatusCodes.Status200OK, ct);
    }
}
