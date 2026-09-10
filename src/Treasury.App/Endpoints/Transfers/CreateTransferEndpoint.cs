using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Treasury.App.Application.Transfers;
using Treasury.App.Contracts.Transactions;
using Treasury.App.Domain;

namespace Treasury.App.Endpoints.Transfers;

public sealed class CreateTransferEndpoint(
    TransferCreationService transferCreationService,
    UserManager<ApplicationUser> userManager)
    : Endpoint<CreateTransferRequest>
{
    public override void Configure()
    {
        Post("/api/transfers");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
    }

    public override async Task HandleAsync(CreateTransferRequest request, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await transferCreationService.CreateAsync(user, request, ct);
        if (!result.Succeeded)
        {
            if (result.Status == TransferCreationStatus.NotFound)
            {
                await SendNotFoundAsync(ct);
                return;
            }

            if (result.Status == TransferCreationStatus.Forbidden)
            {
                await SendForbiddenAsync(ct);
                return;
            }

            foreach (var issue in result.Issues)
            {
                switch (issue.Field)
                {
                    case nameof(CreateTransferRequest.FromAccountId):
                        AddError(x => x.FromAccountId, issue.Message);
                        break;

                    case nameof(CreateTransferRequest.ToAccountId):
                        AddError(x => x.ToAccountId, issue.Message);
                        break;

                    case nameof(CreateTransferRequest.Amount):
                        AddError(x => x.Amount, issue.Message);
                        break;

                    default:
                        AddError(issue.Message);
                        break;
                }
            }

            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var outcome = result.Outcome!;
        await SendAsync(new
        {
            Id = outcome.TransferId,
            outcome.FromAccountId,
            outcome.ToAccountId,
            OutflowTransactionId = outcome.OutflowTransactionId,
            InflowTransactionId = outcome.InflowTransactionId,
            outcome.Amount,
            outcome.Currency,
            outcome.TransferDate
        }, StatusCodes.Status201Created, ct);
    }
}