# Webhooks Transaction API

A minimal .NET 9 service that ingests transaction webhooks from an external provider, ensures idempotency via Redis, persists data in PostgreSQL, and produces a derived `AccountSummary` record after every completed transaction.

---

## Table of Contents

- [How to Run](#how-to-run)
- [Database Schema](#database-schema)
- [API Reference](#api-reference)
- [Request & Response Examples](#request--response-examples)
- [Tests](#tests)
- [Explanation](#explanation)
- [Assumptions](#assumptions)
- [Decision Justification](#decision-justification)
- [Rejected Alternative](#rejected-alternative)
- [Failure Scenario](#failure-scenario)

---

## How to Run

### Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 9.0+ |
| PostgreSQL | 14+ |
| Redis | 7+ (optional — app degrades gracefully without it) |

### 1. Configure connection strings

Edit `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=webhooks_db;Username=postgres;Password=yourpassword",
    "Redis": "localhost:6379,abortConnect=false"
  }
}
```

### 2. Create the database

```sql
CREATE DATABASE webhooks_db;
```

Tables are created automatically on first startup via `EnsureCreated()`.

### 3. Start the app

```bash
dotnet run
```

Swagger UI is available at `http://localhost:<port>/` (root URL).

---

## Database Schema

### `transactions`

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | Primary key |
| `ExternalRef` | `varchar` | Unique — idempotency key |
| `AccountId` | `varchar` | Indexed |
| `Amount` | `numeric(18,2)` | Must be > 0 |
| `Currency` | `char(3)` | ISO 4217, e.g. `USD` |
| `Type` | `varchar(20)` | `credit` or `debit` |
| `Status` | `varchar(20)` | `pending`, `completed`, `failed` |
| `TransactedAt` | `timestamptz` | Provider timestamp |
| `ReceivedAt` | `timestamptz` | Server ingestion time |
| `Metadata` | `jsonb` | Optional provider payload |

### `account_summaries` *(derived)*

| Column | Type | Notes |
|---|---|---|
| `AccountId` | `varchar` | Primary key |
| `TotalCredits` | `numeric(18,2)` | Sum of completed credits |
| `TotalDebits` | `numeric(18,2)` | Sum of completed debits |
| `TransactionCount` | `int` | Count of completed transactions |
| `LastUpdated` | `timestamptz` | Last recompute time |

`RunningBalance` (`TotalCredits - TotalDebits`) is computed in-memory and included in API responses — it is not stored.

---

## API Reference

### `POST /webhooks/transactions`

Ingests a transaction from an external provider.

#### Request body

```json
{
  "externalRef":  "string (required, unique per transaction)",
  "accountId":    "string (required)",
  "amount":       0.0001,
  "currency":     "USD",
  "type":         "credit | debit",
  "status":       "pending | completed | failed",
  "transactedAt": "2026-05-25T10:00:00Z",
  "metadata":     {}
}
```

#### Response codes

| Code | Meaning |
|---|---|
| `201 Created` | Transaction accepted and stored |
| `400 Bad Request` | Validation failed |
| `409 Conflict` | Duplicate `ExternalRef`, or account has a pending transaction in flight |

---

## Request & Response Examples

### ✅ Success — new completed transaction

**Request**
```bash
curl -X POST http://localhost:5208/webhooks/transactions \
  -H "Content-Type: application/json" \
  -d '{
    "externalRef":  "txn_abc123",
    "accountId":    "acc_001",
    "amount":       250.50,
    "currency":     "USD",
    "type":         "credit",
    "status":       "completed",
    "transactedAt": "2026-05-25T19:45:00Z",
    "metadata":     { "source": "retail_deposit" }
  }'
```

**Response `201 Created`**
```json
{
  "id":           "cecb70d2-b4a4-44f1-85b1-57ac3daf7251",
  "externalRef":  "txn_abc123",
  "accountId":    "acc_001",
  "amount":       250.50,
  "currency":     "USD",
  "type":         "credit",
  "status":       "completed",
  "transactedAt": "2026-05-25T19:45:00Z",
  "receivedAt":   "2026-05-25T20:38:18Z",
  "accountSummary": {
    "accountId":        "acc_001",
    "totalCredits":     250.50,
    "totalDebits":      0.00,
    "runningBalance":   250.50,
    "transactionCount": 1,
    "lastUpdated":      "2026-05-25T20:38:18Z"
  }
}
```

---

### ❌ Fail — duplicate ExternalRef

**Request** *(same `externalRef` submitted again)*
```bash
curl -X POST http://localhost:5208/webhooks/transactions \
  -H "Content-Type: application/json" \
  -d '{
    "externalRef":  "txn_abc123",
    "accountId":    "acc_001",
    "amount":       250.50,
    "currency":     "USD",
    "type":         "credit",
    "status":       "completed",
    "transactedAt": "2026-05-25T19:45:00Z"
  }'
```

**Response `409 Conflict`**
```json
{
  "code":    "DUPLICATE_TRANSACTION",
  "message": "Transaction 'txn_abc123' has already been processed."
}
```

---

### ❌ Fail — account has a pending transaction

**Request** *(different `externalRef`, same `accountId`, while a pending transaction exists)*
```bash
curl -X POST http://localhost:5208/webhooks/transactions \
  -H "Content-Type: application/json" \
  -d '{
    "externalRef":  "txn_xyz999",
    "accountId":    "acc_001",
    "amount":       100.00,
    "currency":     "USD",
    "type":         "debit",
    "status":       "pending",
    "transactedAt": "2026-05-25T20:00:00Z"
  }'
```

**Response `409 Conflict`**
```json
{
  "code":    "PENDING_TRANSACTION_EXISTS",
  "message": "Account 'acc_001' already has a pending transaction (cecb70d2-b4a4-44f1-85b1-57ac3daf7251). Please wait for it to complete before submitting a new one."
}
```

---

### ❌ Fail — validation error

**Response `400 Bad Request`**
```json
{
  "errors": {
    "Currency": ["Currency must be a 3-letter ISO code."],
    "Type":     ["Type must be 'credit' or 'debit'."]
  }
}
```

---

## Tests

Tests live in `WebhooksAPI.Tests/`. Run them with:

```bash
dotnet test WebhooksAPI.Tests/WebhooksAPI.Tests.csproj
```

Three unit tests cover the core service logic — no database or Redis required:

| Test | What it verifies |
|---|---|
| `ProcessAsync_NewCompletedTransaction_ReturnsAcceptedAndRefreshesSummary` | Happy path: transaction stored, summary refreshed, Redis key shrunk to 2s TTL |
| `ProcessAsync_DuplicateExternalRef_ReturnsDuplicateRequestWithoutHittingDb` | Redis gate stops duplicate before DB is touched |
| `ProcessAsync_AccountHasPendingTransaction_ReturnsPendingTransactionExists` | Second transaction blocked while first is pending; Redis key released |

---

## Explanation

The service receives a transaction payload from an external provider via `POST /webhooks/transactions`. The processing pipeline has four steps.

**Step 1 — Redis idempotency gate.** Before touching the database, a `SET NX` (set-if-not-exists) is issued against the `ExternalRef`. If the key already exists the request is rejected immediately as a duplicate. Pending transactions hold their key for 30 minutes — the maximum realistic processing window. Completed transactions have their key overwritten with a 2-second TTL via `MarkCompletedAsync`, which absorbs instant retries before the key self-destructs. After that, the PostgreSQL unique constraint on `ExternalRef` permanently prevents re-use.

**Step 2 — Pending-in-flight guard.** If the account already has a `pending` transaction in the database, the new request is rejected with a clear message telling the caller to wait. The Redis key acquired in step 1 is released immediately since nothing was stored.

**Step 3 — Persist.** The transaction is inserted using a Dapper `INSERT ... ON CONFLICT DO NOTHING` upsert. This is the permanent safety net for cases where the Redis TTL has expired.

**Step 4 — Derived computation.** For `completed` transactions only, the `account_summaries` table is recomputed via a single aggregation query that calculates `TotalCredits`, `TotalDebits`, and `TransactionCount`. `RunningBalance` is derived in-memory and never stored.

---

## Assumptions

1. **One pending transaction per account at a time.** The system assumes an account should not have two transactions in flight simultaneously. A second transaction is only allowed once the first reaches `completed` or `failed`.

2. **ExternalRef is globally unique and provider-assigned.** The service treats `ExternalRef` as the canonical idempotency key. It is the provider's responsibility to ensure uniqueness across all transactions.

3. **Status transitions are provider-driven.** This service only ingests — it does not manage status transitions. A `pending` transaction becomes `completed` or `failed` when the provider sends a new webhook with the updated status and a different `ExternalRef`.

---

## Decision Justification

**1. Redis for idempotency, PostgreSQL as the safety net.**
Redis `SET NX` is O(1) and avoids a database round-trip on every duplicate request. However, Redis is volatile — a restart or eviction could lose a key. The PostgreSQL unique constraint on `ExternalRef` ensures correctness even when Redis is unavailable. The two layers complement each other: Redis is fast, PostgreSQL is durable.

**2. Dapper for writes, EF Core for schema management.**
EF Core's `EnsureCreated()` handles schema creation without requiring a migration runner, which keeps the setup minimal. Dapper is used for the upsert and aggregation queries because `INSERT ... ON CONFLICT` and `SUM ... FILTER` are idiomatic SQL that EF Core cannot express cleanly without raw SQL anyway. Using Dapper directly keeps those queries readable and explicit.

---

## Rejected Alternative

**Outbox pattern for derived computation.** The `AccountSummary` refresh could be decoupled into an outbox table and processed asynchronously by a background worker. This would improve write throughput and resilience. It was rejected because it adds significant complexity (background service, outbox table, at-least-once delivery logic) that is not justified for a single derived aggregate. The synchronous in-request refresh is simple, correct, and fast enough for this use case.

---

## Failure Scenario

**Redis is down when a completed transaction is re-submitted.**

1. The completed transaction was stored in PostgreSQL. Its Redis key expired after 2 seconds.
2. Redis goes offline.
3. The provider retries the same `ExternalRef`.
4. `TryAcquireAsync` catches the `RedisException`, logs a warning, and returns `true` (treats as unseen).
5. The request proceeds to the DB upsert.
6. PostgreSQL's `ON CONFLICT ("ExternalRef") DO NOTHING` fires — nothing is inserted.
7. The repo detects no row was returned, fetches the existing row, and sets `wasDuplicate = true`.
8. The service returns `ProcessResult.DuplicateRequest` → the controller responds `409 Conflict`.

**Outcome:** No duplicate is stored. The caller receives a correct rejection. The only cost is one extra DB read. The system is correct without Redis.
