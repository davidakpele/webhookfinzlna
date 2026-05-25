using WebhooksAPI.Data.Models;

namespace WebhooksAPI.Data.Repositories;

public interface ITransactionRepository
{
    /// <summary>
    /// Inserts the transaction. If ExternalRef already exists returns the existing row
    /// with wasDuplicate = true. Uses ON CONFLICT DO NOTHING for atomicity.
    /// </summary>
    Task<(Transaction transaction, bool wasDuplicate)> UpsertAsync(Transaction txn);

    /// <summary>
    /// Returns the first pending transaction for the account, or null if none exists.
    /// Used to block a second transaction while one is still in flight.
    /// </summary>
    Task<Transaction?> GetPendingTransactionAsync(string accountId);

    /// <summary>
    /// Recomputes and upserts the AccountSummary for the given account
    /// based on all completed transactions.
    /// </summary>
    Task RefreshAccountSummaryAsync(string accountId);

    Task<AccountSummary?> GetSummaryAsync(string accountId);
}
