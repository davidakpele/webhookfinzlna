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
    /// <summary>Ingest a transaction from an external provider.</summary>
    /// <remarks>
    /// **Idempotency:** Keyed on <c>externalRef</c>. Submitting the same ref twice returns 409.
    ///
    /// **Pending guard:** If the account already has a pending transaction, the request is
    /// rejected with 409 until the first one completes or fails.
    ///
    /// **Completed transactions:** The Redis idempotency key is deleted on completion so the
    /// slot is freed immediately rather than waiting for the 30-day TTL.
    ///
    /// **Sample request:**
    /// ```json
    /// {
    ///   "externalRef": "txn_abc123",
    ///   "accountId":   "acc_001",
    ///   "amount":      250.00,
    ///   "currency":    "USD",
    ///   "type":        "credit",
    ///   "status":      "completed",
    ///   "transactedAt":"2026-05-25T10:00:00Z"
    /// }
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
