using Moq;
using System.Text.Json;
using WebhooksAPI.Data.DTO;
using WebhooksAPI.Data.Models;
using WebhooksAPI.Data.Repositories;
using WebhooksAPI.Data.Services;

namespace WebhooksAPI.Tests;

public class TransactionServiceTests
{
    // ── Shared helpers ────────────────────────────────────────────────────────

    private static InboundTransactionDto BuildDto(
        string externalRef = "txn_001",
        string accountId   = "acc_001",
        string status      = "completed") => new()
    {
        ExternalRef  = externalRef,
        AccountId    = accountId,
        Amount       = 100m,
        Currency     = "USD",
        Type         = "credit",
        Status       = status,
        TransactedAt = DateTime.UtcNow,
        Metadata     = JsonDocument.Parse("{}").RootElement
    };

    private static Transaction BuildSavedTransaction(InboundTransactionDto dto) => new()
    {
        Id           = Guid.NewGuid(),
        ExternalRef  = dto.ExternalRef,
        AccountId    = dto.AccountId,
        Amount       = dto.Amount,
        Currency     = dto.Currency,
        Type         = dto.Type,
        Status       = dto.Status,
        TransactedAt = dto.TransactedAt,
        ReceivedAt   = DateTime.UtcNow
    };

    // ── Test 1: New completed transaction is accepted and summary is refreshed ─

    [Fact]
    public async Task ProcessAsync_NewCompletedTransaction_ReturnsAcceptedAndRefreshesSummary()
    {
        // Arrange
        var dto         = BuildDto(status: "completed");
        var savedTxn    = BuildSavedTransaction(dto);

        var idempotency = new Mock<IIdempotencyService>();
        idempotency
            .Setup(x => x.TryAcquireAsync(dto.ExternalRef, It.IsAny<TimeSpan>()))
            .ReturnsAsync(true); // first time seen

        var repo = new Mock<ITransactionRepository>();
        repo.Setup(x => x.GetPendingTransactionAsync(dto.AccountId))
            .ReturnsAsync((Transaction?)null); // no pending transaction

        repo.Setup(x => x.UpsertAsync(It.IsAny<Transaction>()))
            .ReturnsAsync((savedTxn, false)); // inserted, not duplicate

        repo.Setup(x => x.GetSummaryAsync(dto.AccountId))
            .ReturnsAsync(new AccountSummary
            {
                AccountId        = dto.AccountId,
                TotalCredits     = 100m,
                TotalDebits      = 0m,
                TransactionCount = 1,
                LastUpdated      = DateTime.UtcNow
            });

        var service = new TransactionService(idempotency.Object, repo.Object);

        // Act
        var result = await service.ProcessAsync(dto);

        // Assert
        var accepted = Assert.IsType<ProcessResult.Accepted>(result);
        Assert.Equal(dto.ExternalRef, accepted.Transaction.ExternalRef);
        Assert.Equal("completed", accepted.Transaction.Status);
        Assert.Equal(100m, accepted.Transaction.AccountSummary.TotalCredits);

        // Summary must be refreshed for completed transactions
        repo.Verify(x => x.RefreshAccountSummaryAsync(dto.AccountId), Times.Once);

        // Redis key must be shrunk to 2-second TTL after completion
        idempotency.Verify(x => x.MarkCompletedAsync(dto.ExternalRef), Times.Once);
    }

    // ── Test 2: Duplicate ExternalRef is rejected before touching the DB ──────

    [Fact]
    public async Task ProcessAsync_DuplicateExternalRef_ReturnsDuplicateRequestWithoutHittingDb()
    {
        // Arrange
        var dto = BuildDto();

        var idempotency = new Mock<IIdempotencyService>();
        idempotency
            .Setup(x => x.TryAcquireAsync(dto.ExternalRef, It.IsAny<TimeSpan>()))
            .ReturnsAsync(false); // key already exists in Redis

        var repo = new Mock<ITransactionRepository>();

        var service = new TransactionService(idempotency.Object, repo.Object);

        // Act
        var result = await service.ProcessAsync(dto);

        // Assert
        var duplicate = Assert.IsType<ProcessResult.DuplicateRequest>(result);
        Assert.Equal(dto.ExternalRef, duplicate.ExternalRef);

        // DB must never be touched — Redis gate stopped it
        repo.Verify(x => x.UpsertAsync(It.IsAny<Transaction>()), Times.Never);
        repo.Verify(x => x.GetPendingTransactionAsync(It.IsAny<string>()), Times.Never);
    }

    // ── Test 3: Second transaction blocked while account has a pending one ─────

    [Fact]
    public async Task ProcessAsync_AccountHasPendingTransaction_ReturnsPendingTransactionExists()
    {
        // Arrange
        var dto        = BuildDto(externalRef: "txn_002", status: "pending");
        var pendingTxn = BuildSavedTransaction(BuildDto(externalRef: "txn_001", status: "pending"));

        var idempotency = new Mock<IIdempotencyService>();
        idempotency
            .Setup(x => x.TryAcquireAsync(dto.ExternalRef, It.IsAny<TimeSpan>()))
            .ReturnsAsync(true); // new ExternalRef, passes Redis gate

        var repo = new Mock<ITransactionRepository>();
        repo.Setup(x => x.GetPendingTransactionAsync(dto.AccountId))
            .ReturnsAsync(pendingTxn); // account already has a pending transaction

        var service = new TransactionService(idempotency.Object, repo.Object);

        // Act
        var result = await service.ProcessAsync(dto);

        // Assert
        var blocked = Assert.IsType<ProcessResult.PendingTransactionExists>(result);
        Assert.Equal(dto.AccountId, blocked.AccountId);
        Assert.Equal(pendingTxn.Id, blocked.PendingTransactionId);

        // Redis key must be released — we didn't store anything
        idempotency.Verify(x => x.ReleaseAsync(dto.ExternalRef), Times.Once);

        // DB insert must never happen
        repo.Verify(x => x.UpsertAsync(It.IsAny<Transaction>()), Times.Never);
    }
}
