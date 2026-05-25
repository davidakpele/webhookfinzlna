using System.Text.Json;
using WebhooksAPI.Data.DTO;
using WebhooksAPI.Data.Models;
using WebhooksAPI.Data.Repositories;

namespace WebhooksAPI.Data.Services;

public class TransactionService(
    IIdempotencyService idempotency,
    ITransactionRepository repo) : ITransactionService
{
    // Keys live in Redis for 30 days — long enough to cover any realistic retry window
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromDays(30);

    public async Task<TransactionResponse> ProcessAsync(InboundTransactionDto dto)
    {
        // 1. Fast Redis gate — reject duplicates before touching the DB
        bool isNew = await idempotency.TryAcquireAsync(dto.ExternalRef, IdempotencyTtl);

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
            Metadata     = dto.Metadata.HasValue
                               ? dto.Metadata.Value.GetRawText()
                               : null
        };

        // 2. DB upsert — ON CONFLICT DO NOTHING is the safety net if Redis TTL expired
        var (saved, wasDuplicate) = await repo.UpsertAsync(txn);

        // 3. Derived computation: refresh account summary for completed transactions only
        if (!wasDuplicate && saved.Status == "completed")
            await repo.RefreshAccountSummaryAsync(saved.AccountId);

        // 4. Fetch the latest summary to include in the response
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

        return new TransactionResponse(
            saved.Id,
            saved.ExternalRef,
            saved.AccountId,
            saved.Amount,
            saved.Currency,
            saved.Type,
            saved.Status,
            saved.TransactedAt,
            saved.ReceivedAt,
            summaryDto,
            wasDuplicate || !isNew);
    }
}
