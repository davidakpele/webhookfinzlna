using Dapper;
using Npgsql;
using WebhooksAPI.Data.Models;

namespace WebhooksAPI.Data.Repositories;

public class TransactionRepository(IConfiguration config) : ITransactionRepository
{
    private NpgsqlConnection CreateConnection() =>
        new(config.GetConnectionString("PostgreSQL"));

    public async Task<(Transaction transaction, bool wasDuplicate)> UpsertAsync(Transaction txn)
    {
        const string sql = """
            INSERT INTO transactions
                (id, external_ref, account_id, amount, currency, type, status,
                 transacted_at, received_at, metadata)
            VALUES
                (@Id, @ExternalRef, @AccountId, @Amount, @Currency, @Type, @Status,
                 @TransactedAt, @ReceivedAt, @Metadata::jsonb)
            ON CONFLICT (external_ref) DO NOTHING
            RETURNING *;
            """;

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        var inserted = await conn.QuerySingleOrDefaultAsync<Transaction>(sql, txn);

        if (inserted is not null)
            return (inserted, false);

        // Duplicate — fetch the existing row so the caller can return it
        var existing = await conn.QuerySingleAsync<Transaction>(
            "SELECT * FROM transactions WHERE external_ref = @ExternalRef",
            new { txn.ExternalRef });

        return (existing, true);
    }

    public async Task RefreshAccountSummaryAsync(string accountId)
    {
        // Derived computation: aggregate completed transactions → upsert summary
        const string sql = """
            INSERT INTO account_summaries
                (account_id, total_credits, total_debits, transaction_count, last_updated)
            SELECT
                account_id,
                COALESCE(SUM(amount) FILTER (WHERE type = 'credit'), 0) AS total_credits,
                COALESCE(SUM(amount) FILTER (WHERE type = 'debit'),  0) AS total_debits,
                COUNT(*)                                                 AS transaction_count,
                NOW()                                                    AS last_updated
            FROM transactions
            WHERE account_id = @AccountId
              AND status     = 'completed'
            GROUP BY account_id
            ON CONFLICT (account_id) DO UPDATE
                SET total_credits     = EXCLUDED.total_credits,
                    total_debits      = EXCLUDED.total_debits,
                    transaction_count = EXCLUDED.transaction_count,
                    last_updated      = EXCLUDED.last_updated;
            """;

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(sql, new { AccountId = accountId });
    }

    public async Task<AccountSummary?> GetSummaryAsync(string accountId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<AccountSummary>(
            "SELECT * FROM account_summaries WHERE account_id = @AccountId",
            new { AccountId = accountId });
    }
}
