using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WebhooksAPI.Data.Models;

namespace WebhooksAPI.Configurations;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<AccountSummary> AccountSummaries => Set<AccountSummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        modelBuilder.Entity<Transaction>(e =>
        {
            e.ToTable("transactions");
            e.HasKey(t => t.Id);

            // Explicit snake_case column names — Dapper SQL must match these exactly
            e.Property(t => t.Id).HasColumnName("id");
            e.Property(t => t.ExternalRef).HasColumnName("external_ref");
            e.Property(t => t.AccountId).HasColumnName("account_id");
            e.Property(t => t.Amount).HasColumnName("amount").HasPrecision(18, 2);
            e.Property(t => t.Currency).HasColumnName("currency").HasMaxLength(3);
            e.Property(t => t.Type).HasColumnName("type").HasMaxLength(20);
            e.Property(t => t.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(t => t.TransactedAt).HasColumnName("transacted_at").HasConversion(utcConverter);
            e.Property(t => t.ReceivedAt).HasColumnName("received_at").HasConversion(utcConverter);
            e.Property(t => t.Metadata).HasColumnName("metadata");

            e.HasIndex(t => t.ExternalRef)
             .IsUnique()
             .HasDatabaseName("idx_transactions_external_ref");

            e.HasIndex(t => t.AccountId)
             .HasDatabaseName("idx_transactions_account_id");
        });

        modelBuilder.Entity<AccountSummary>(e =>
        {
            e.ToTable("account_summaries");
            e.HasKey(a => a.AccountId);

            e.Property(a => a.AccountId).HasColumnName("account_id");
            e.Property(a => a.TotalCredits).HasColumnName("total_credits").HasPrecision(18, 2);
            e.Property(a => a.TotalDebits).HasColumnName("total_debits").HasPrecision(18, 2);
            e.Property(a => a.TransactionCount).HasColumnName("transaction_count");
            e.Property(a => a.LastUpdated).HasColumnName("last_updated").HasConversion(utcConverter);

            e.Ignore(a => a.RunningBalance);
        });
    }
}
