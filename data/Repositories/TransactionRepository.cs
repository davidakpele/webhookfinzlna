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
                ("Id", "ExternalRef", "AccountId", "Amount", "Currency", "Type", "Status",
                 "TransactedAt", "ReceivedAt", "Metadata")
            VALUES
                (@Id, @ExternalRef, @AccountId, @Amount, @Currency, @Type, @Status,
                 @TransactedAt, @ReceivedAt, @Metadata::jsonb)
            ON CONFLICT ("ExternalRef") DO NOTHING
            RETURNING *;
            """;

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        var inserted = await conn.QuerySingleOrDefaultAsync<Transaction>(sql, txn);

        if (inserted is not null)
            return (inserted, false);

        // Duplicate — return the existing row
        var existing = await conn.QuerySingleAsync<Transaction>(
            """SELECT * FROM transactions WHERE "ExternalRef" = @ExternalRef""",
            new { txn.ExternalRef });

        return (existing, true);
    }

    public async Task<Transaction?> GetPendingTransactionAsync(string accountId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<Transaction>(
            """
            SELECT * FROM transactions
            WHERE "AccountId" = @AccountId
              AND "Status"    = 'pending'
            LIMIT 1
            """,
            new { AccountId = accountId });
    }

    public async Task RefreshAccountSummaryAsync(string accountId)
    {
        const string sql = """
            INSERT INTO account_summaries
                ("AccountId", "TotalCredits", "TotalDebits", "TransactionCount", "LastUpdated")
            SELECT
                "AccountId",
                COALESCE(SUM("Amount") FILTER (WHERE "Type" = 'credit'), 0),
                COALESCE(SUM("Amount") FILTER (WHERE "Type" = 'debit'),  0),
                COUNT(*),
                NOW()
            FROM transactions
            WHERE "AccountId" = @AccountId
              AND "Status"    = 'completed'
            GROUP BY "AccountId"
            ON CONFLICT ("AccountId") DO UPDATE
                SET "TotalCredits"     = EXCLUDED."TotalCredits",
                    "TotalDebits"      = EXCLUDED."TotalDebits",
                    "TransactionCount" = EXCLUDED."TransactionCount",
                    "LastUpdated"      = EXCLUDED."LastUpdated";
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
            """SELECT * FROM account_summaries WHERE "AccountId" = @AccountId""",
            new { AccountId = accountId });
    }
}
