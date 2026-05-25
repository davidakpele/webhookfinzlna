using Microsoft.AspNetCore.Mvc;
using WebhooksAPI.Data.DTO;
using WebhooksAPI.Data.Services;

namespace WebhooksAPI.Controllers;

[ApiController]
[Route("webhooks")]
public class WebhooksController(ITransactionService transactionService) : ControllerBase
{
    /// <summary>
    /// Ingests a transaction from an external provider.
    /// Idempotent: re-submitting the same ExternalRef returns the original record.
    /// </summary>
    [HttpPost("transactions")]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> IngestTransaction([FromBody] InboundTransactionDto dto)
    {
        var result = await transactionService.ProcessAsync(dto);

        // 200 for duplicates, 201 for new records — lets callers distinguish without parsing the body
        return result.WasDuplicate
            ? Ok(result)
            : CreatedAtAction(nameof(IngestTransaction), result);
    }
}
