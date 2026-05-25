using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebhooksAPI.data.Repositories
{
   
public class TransactionRepository
{
    private readonly DbConnectionFactory _db;
 
    public TransactionRepository(DbConnectionFactory db) => _db = db;
        public async Task<(Transaction transaction, bool wasDuplicate)> UpsertAsync(Transaction txn)
        {
            const string sql = """
                INSERT INTO transactions
                    (id, external_ref, account_id, amount, currency, type, status, transacted_at, received_at, metadata)
                VALUES
                    (@Id, @ExternalRef, @AccountId, @Amount, @Currency, @Type, @Status, @TransactedAt, @ReceivedAt, @Metadata::jsonb)
                ON CONFLICT (external_ref) DO NOTHING
                RETURNING *;
                """;
    
            using var conn = _db.Create();
            conn.Open();
    
            var inserted = await conn.QuerySingleOrDefaultAsync<Transaction>(sql, txn);
    
            if (inserted is not null)
                return (inserted, false);
    
            // Duplicate: fetch existing row so we can return it
            var existing = await conn.QuerySingleAsync<Transaction>(
                "SELECT * FROM transactions WHERE external_ref = @ExternalRef",
                new { txn.ExternalRef });
    
            return (existing, true);
        }
    
        public async Task RefreshAccountSummaryAsync(string accountId)
        {
            const string sql = """
                INSERT INTO account_summaries (account_id, total_credits, total_debits, transaction_count, last_updated)
                SELECT
                    account_id,
                    SUM(amount) FILTER (WHERE type = 'credit')          AS total_credits,
                    SUM(amount) FILTER (WHERE type = 'debit')           AS total_debits,
                    COUNT(*)                                             AS transaction_count,
                    NOW()                                               AS last_updated
                FROM transactions
                WHERE account_id = @AccountId AND status = 'completed'
                GROUP BY account_id
                ON CONFLICT (account_id) DO UPDATE
                    SET total_credits      = EXCLUDED.total_credits,
                        total_debits       = EXCLUDED.total_debits,
                        transaction_count  = EXCLUDED.transaction_count,
                        last_updated       = EXCLUDED.last_updated;
                """;
    
            using var conn = _db.Create();
            conn.Open();
            await conn.ExecuteAsync(sql, new { AccountId = accountId });
        }
    
        public async Task<AccountSummary?> GetSummaryAsync(string accountId)
        {
            using var conn = _db.Create();
            conn.Open();
            return await conn.QuerySingleOrDefaultAsync<AccountSummary>(
                "SELECT * FROM account_summaries WHERE account_id = @AccountId",
                new { AccountId = accountId });
        }
    }
    
}