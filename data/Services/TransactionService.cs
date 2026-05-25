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
        // ── 1. Redis gate — block duplicate ExternalRef ───────────────────────
        // Pending TTL = 30 min. Completed keys are overwritten with a 2s TTL
        // by MarkCompletedAsync, then the DB unique constraint takes over.
        bool acquired = await idempotency.TryAcquireAsync(dto.ExternalRef, IdempotencyService.PendingTtl);
        if (!acquired)
            return new ProcessResult.DuplicateRequest(dto.ExternalRef);

        // ── 2. Pending-in-flight guard ────────────────────────────────────────
        // Prevent a second transaction for the same account while one is pending.
        var pending = await repo.GetPendingTransactionAsync(dto.AccountId);
        if (pending is not null)
        {
            await idempotency.ReleaseAsync(dto.ExternalRef); // free the slot we just took
            return new ProcessResult.PendingTransactionExists(dto.AccountId, pending.Id);
        }

        // ── 3. Persist ────────────────────────────────────────────────────────
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

        // DB caught a duplicate the Redis TTL missed — treat as invalid re-use
        if (wasDuplicate)
        {
            await idempotency.ReleaseAsync(dto.ExternalRef);
            return new ProcessResult.DuplicateRequest(dto.ExternalRef);
        }

        // ── 4. Post-save: derived computation + Redis TTL update ──────────────
        if (saved.Status == "completed")
        {
            await repo.RefreshAccountSummaryAsync(saved.AccountId);
            // Shrink the Redis key to 2 s — absorbs instant retries, then expires.
            // The DB unique constraint on ExternalRef permanently blocks re-use after that.
            await idempotency.MarkCompletedAsync(saved.ExternalRef);
        }
        // pending/failed keys keep their 30-min TTL set in step 1

        // ── 5. Build response ─────────────────────────────────────────────────
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
