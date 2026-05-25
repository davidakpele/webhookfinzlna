using WebhooksAPI.Data.Models;

namespace WebhooksAPI.Data.Repositories;

public interface ITransactionRepository
{
    /// <summary>
    /// Inserts the transaction. If external_ref already exists, returns the existing row
    /// and sets wasDuplicate = true. Uses ON CONFLICT DO NOTHING for atomicity.
    /// </summary>
    Task<(Transaction transaction, bool wasDuplicate)> UpsertAsync(Transaction txn);

    /// <summary>
    /// Recomputes and upserts the AccountSummary for the given account
    /// based on all completed transactions.
    /// </summary>
    Task RefreshAccountSummaryAsync(string accountId);

    Task<AccountSummary?> GetSummaryAsync(string accountId);
}
