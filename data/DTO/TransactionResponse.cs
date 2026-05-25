namespace WebhooksAPI.Data.DTO;

public record TransactionResponse(
    Guid Id,
    string ExternalRef,
    string AccountId,
    decimal Amount,
    string Currency,
    string Type,
    string Status,
    DateTime TransactedAt,
    DateTime ReceivedAt,
    AccountSummaryDto AccountSummary
);

public record AccountSummaryDto(
    string AccountId,
    decimal TotalCredits,
    decimal TotalDebits,
    decimal RunningBalance,
    int TransactionCount,
    DateTime LastUpdated
);

public abstract record ProcessResult
{
 
    public record Accepted(TransactionResponse Transaction) : ProcessResult;

    public record DuplicateRequest(string ExternalRef) : ProcessResult;
    public record PendingTransactionExists(string AccountId, Guid PendingTransactionId) : ProcessResult;
}
