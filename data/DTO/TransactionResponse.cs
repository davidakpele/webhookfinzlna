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
    AccountSummaryDto AccountSummary,
    bool WasDuplicate
);

public record AccountSummaryDto(
    string AccountId,
    decimal TotalCredits,
    decimal TotalDebits,
    decimal RunningBalance,
    int TransactionCount,
    DateTime LastUpdated
);
