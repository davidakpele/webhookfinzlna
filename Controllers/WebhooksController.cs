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
    /// Idempotent — re-submitting the same <c>ExternalRef</c> returns the original record
    /// with <c>wasDuplicate: true</c> and HTTP 200 instead of 201.
    ///
    /// **Flow:**
    /// 1. Redis SET NX check (fast duplicate gate)
    /// 2. PostgreSQL upsert with `ON CONFLICT DO NOTHING` (safety net)
    /// 3. If new + completed → recompute `AccountSummary` (derived record)
    /// 4. Return transaction + latest account summary
    ///
    /// **Sample request:**
    /// ```json
    /// {
    ///   "externalRef": "txn_abc123",
    ///   "accountId": "acc_001",
    ///   "amount": 250.00,
    ///   "currency": "USD",
    ///   "type": "credit",
    ///   "status": "completed",
    ///   "transactedAt": "2026-05-25T10:00:00Z"
    /// }
    /// ```
    /// </remarks>
    /// <param name="dto">Transaction payload from the provider.</param>
    /// <response code="201">Transaction accepted and stored. Account summary updated.</response>
    /// <response code="200">Duplicate — transaction already exists. Original record returned.</response>
    /// <response code="400">Validation failed (missing fields, bad currency code, invalid type/status).</response>
    [HttpPost("transactions")]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> IngestTransaction([FromBody] InboundTransactionDto dto)
    {
        var result = await transactionService.ProcessAsync(dto);

        // 201 for new records, 200 for duplicates — callers can distinguish without parsing the body
        return result.WasDuplicate
            ? Ok(result)
            : CreatedAtAction(nameof(IngestTransaction), result);
    }
}
