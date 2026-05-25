using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebhooksAPI.data.DTO
{
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
        bool WasDuplicate
    );
 
}