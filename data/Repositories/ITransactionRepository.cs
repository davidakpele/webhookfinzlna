using WebhooksAPI.Data.Models;

namespace WebhooksAPI.Data.Repositories;

public interface ITransactionRepository
{

    Task<(Transaction transaction, bool wasDuplicate)> UpsertAsync(Transaction txn);

    Task<Transaction?> GetPendingTransactionAsync(string accountId);

    Task RefreshAccountSummaryAsync(string accountId);

    Task<AccountSummary?> GetSummaryAsync(string accountId);
}
