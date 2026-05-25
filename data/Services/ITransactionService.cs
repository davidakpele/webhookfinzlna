using WebhooksAPI.Data.DTO;

namespace WebhooksAPI.Data.Services;

public interface ITransactionService
{
    Task<TransactionResponse> ProcessAsync(InboundTransactionDto dto);
}
