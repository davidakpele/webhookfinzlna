using Microsoft.AspNetCore.Mvc;
using WebhooksAPI.Data.DTO;
using WebhooksAPI.Data.Services;

namespace WebhooksAPI.Controllers;

/// <summary>Handles inbound webhook events from external payment providers.</summary>
[ApiController]
[Route("webhooks")]
[Produces("application/json")]
public class WebhooksController(ITransactionService transactionService) : ControllerBase
{
   
    /// ```
    /// </remarks>
    /// <param name="dto">Transaction payload from the provider.</param>
    /// <response code="201">Transaction accepted and stored.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="409">Duplicate transaction, or account has a pending transaction in flight.</response>
    [HttpPost("transactions")]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> IngestTransaction([FromBody] InboundTransactionDto dto)
    {
        var result = await transactionService.ProcessAsync(dto);

        return result switch
        {
            ProcessResult.Accepted r =>
                CreatedAtAction(nameof(IngestTransaction), r.Transaction),

            ProcessResult.DuplicateRequest r =>
                Conflict(new ErrorResponse(
                    "DUPLICATE_TRANSACTION",
                    $"Transaction '{r.ExternalRef}' has already been processed.")),

            ProcessResult.PendingTransactionExists r =>
                Conflict(new ErrorResponse(
                    "PENDING_TRANSACTION_EXISTS",
                    $"Account '{r.AccountId}' already has a pending transaction ({r.PendingTransactionId}). " +
                    "Please wait for it to complete before submitting a new one.")),

            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
