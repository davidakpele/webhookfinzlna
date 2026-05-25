using WebhooksAPI.Data.DTO;
using WebhooksAPI.Data.Models;
using WebhooksAPI.Data.Repositories;

namespace WebhooksAPI.Data.Services;

public class TransactionService(
    IIdempotencyService idempotency,
    ITransactionRepository repo) : ITransactionService
{
    public async Task<ProcessResult> ProcessAsync(InboundTransactionDto dto)
    {

        bool acquired = await idempotency.TryAcquireAsync(dto.ExternalRef, IdempotencyService.PendingTtl);
        if (!acquired)
            return new ProcessResult.DuplicateRequest(dto.ExternalRef);

        var pending = await repo.GetPendingTransactionAsync(dto.AccountId);
        if (pending is not null)
        {
            await idempotency.ReleaseAsync(dto.ExternalRef); // free the slot we just took
            return new ProcessResult.PendingTransactionExists(dto.AccountId, pending.Id);
        }

        var txn = new Transaction
        {
            Id           = Guid.NewGuid(),
            ExternalRef  = dto.ExternalRef,
            AccountId    = dto.AccountId,
            Amount       = dto.Amount,
            Currency     = dto.Currency.ToUpperInvariant(),
            Type         = dto.Type.ToLowerInvariant(),
            Status       = dto.Status.ToLowerInvariant(),
            TransactedAt = DateTime.SpecifyKind(dto.TransactedAt, DateTimeKind.Utc),
            ReceivedAt   = DateTime.UtcNow,
            Metadata     = dto.Metadata.HasValue ? dto.Metadata.Value.GetRawText() : null
        };

        var (saved, wasDuplicate) = await repo.UpsertAsync(txn);


        if (wasDuplicate)
        {
            await idempotency.ReleaseAsync(dto.ExternalRef);
            return new ProcessResult.DuplicateRequest(dto.ExternalRef);
        }

        if (saved.Status == "completed")
        {
            await repo.RefreshAccountSummaryAsync(saved.AccountId);

            await idempotency.MarkCompletedAsync(saved.ExternalRef);
        }

        var summary = await repo.GetSummaryAsync(saved.AccountId);

        var summaryDto = summary is not null
            ? new AccountSummaryDto(
                summary.AccountId,
                summary.TotalCredits,
                summary.TotalDebits,
                summary.RunningBalance,
                summary.TransactionCount,
                summary.LastUpdated)
            : new AccountSummaryDto(saved.AccountId, 0, 0, 0, 0, DateTime.UtcNow);

        return new ProcessResult.Accepted(new TransactionResponse(
            saved.Id,
            saved.ExternalRef,
            saved.AccountId,
            saved.Amount,
            saved.Currency,
            saved.Type,
            saved.Status,
            saved.TransactedAt,
            saved.ReceivedAt,
            summaryDto));
    }
}
