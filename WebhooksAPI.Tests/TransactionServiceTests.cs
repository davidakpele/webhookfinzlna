using Moq;
using System.Text.Json;
using WebhooksAPI.Data.DTO;
using WebhooksAPI.Data.Models;
using WebhooksAPI.Data.Repositories;
using WebhooksAPI.Data.Services;
using Xunit;

namespace WebhooksAPI.Tests;

public class TransactionServiceTests
{
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
        TransactedAt = DateTime.UtcNow
    };

    private static Transaction BuildTransaction(string externalRef, string accountId, string status) => new()
    {
        Id           = Guid.NewGuid(),
        ExternalRef  = externalRef,
        AccountId    = accountId,
        Amount       = 100m,
        Currency     = "USD",
        Type         = "credit",
        Status       = status,
        TransactedAt = DateTime.UtcNow,
        ReceivedAt   = DateTime.UtcNow
    };

    private static void Fail(string message) =>
        throw new Exception($"Test failed: {message}");

    [Fact]
    public async Task NewCompletedTransaction_ReturnsAccepted_AndRefreshesSummary()
    {
        var dto     = BuildDto(status: "completed");
        var savedTxn = BuildTransaction(dto.ExternalRef, dto.AccountId, "completed");

        var idempotency = new Mock<IIdempotencyService>();
        idempotency
            .Setup(x => x.TryAcquireAsync(dto.ExternalRef, It.IsAny<TimeSpan>()))
            .ReturnsAsync(true);

        var repo = new Mock<ITransactionRepository>();
        repo.Setup(x => x.GetPendingTransactionAsync(dto.AccountId))
            .ReturnsAsync((Transaction?)null);

        repo.Setup(x => x.UpsertAsync(It.IsAny<Transaction>()))
            .ReturnsAsync((savedTxn, false));

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

        var result = await service.ProcessAsync(dto);
        if (result is not ProcessResult.Accepted accepted)
            Fail($"Expected Accepted but got {result.GetType().Name}");
        else
        {
            if (accepted.Transaction.ExternalRef != dto.ExternalRef)
                Fail($"ExternalRef mismatch: expected {dto.ExternalRef}, got {accepted.Transaction.ExternalRef}");

            if (accepted.Transaction.Status != "completed")
                Fail($"Status mismatch: expected 'completed', got {accepted.Transaction.Status}");

            if (accepted.Transaction.AccountSummary.TotalCredits != 100m)
                Fail($"TotalCredits mismatch: expected 100, got {accepted.Transaction.AccountSummary.TotalCredits}");
        }
        repo.Verify(x => x.RefreshAccountSummaryAsync(dto.AccountId), Times.Once);
        idempotency.Verify(x => x.MarkCompletedAsync(dto.ExternalRef), Times.Once);
    }

    [Fact]
    public async Task DuplicateExternalRef_ReturnsDuplicateResult_DbNeverCalled()
    {
        var dto = BuildDto();

        var idempotency = new Mock<IIdempotencyService>();
        idempotency
            .Setup(x => x.TryAcquireAsync(dto.ExternalRef, It.IsAny<TimeSpan>()))
            .ReturnsAsync(false);

        var repo = new Mock<ITransactionRepository>();

        var service = new TransactionService(idempotency.Object, repo.Object);

        // Act
        var result = await service.ProcessAsync(dto);
        if (result is not ProcessResult.DuplicateRequest duplicate)
            Fail($"Expected DuplicateRequest but got {result.GetType().Name}");
        else if (duplicate.ExternalRef != dto.ExternalRef)
            Fail($"ExternalRef mismatch: expected {dto.ExternalRef}, got {duplicate.ExternalRef}");

        repo.Verify(x => x.UpsertAsync(It.IsAny<Transaction>()), Times.Never);
        repo.Verify(x => x.GetPendingTransactionAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AccountHasPendingTransaction_BlocksNewRequest_ReleasesRedisKey()
    {
        var dto        = BuildDto(externalRef: "txn_002", status: "pending");
        var pendingTxn = BuildTransaction("txn_001", dto.AccountId, "pending");

        var idempotency = new Mock<IIdempotencyService>();
        idempotency
            .Setup(x => x.TryAcquireAsync(dto.ExternalRef, It.IsAny<TimeSpan>()))
            .ReturnsAsync(true);

        var repo = new Mock<ITransactionRepository>();
        repo.Setup(x => x.GetPendingTransactionAsync(dto.AccountId))
            .ReturnsAsync(pendingTxn); 
        var service = new TransactionService(idempotency.Object, repo.Object);

        // Act
        var result = await service.ProcessAsync(dto);
        if (result is not ProcessResult.PendingTransactionExists blocked)
            Fail($"Expected PendingTransactionExists but got {result.GetType().Name}");
        else
        {
            if (blocked.AccountId != dto.AccountId)
                Fail($"AccountId mismatch: expected {dto.AccountId}, got {blocked.AccountId}");

            if (blocked.PendingTransactionId != pendingTxn.Id)
                Fail($"PendingTransactionId mismatch: expected {pendingTxn.Id}, got {blocked.PendingTransactionId}");
        }

        idempotency.Verify(x => x.ReleaseAsync(dto.ExternalRef), Times.Once);

        repo.Verify(x => x.UpsertAsync(It.IsAny<Transaction>()), Times.Never);
    }
}
