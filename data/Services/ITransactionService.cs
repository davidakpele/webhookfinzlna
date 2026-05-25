using WebhooksAPI.Data.DTO;

namespace WebhooksAPI.Data.Services;

public interface ITransactionService
{
    Task<ProcessResult> ProcessAsync(InboundTransactionDto dto);
}
