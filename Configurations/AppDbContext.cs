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

            // Explicit column names — Dapper SQL must match these exactly
            e.Property(t => t.Id).HasColumnName("Id");
            e.Property(t => t.ExternalRef).HasColumnName("ExternalRef");
            e.Property(t => t.AccountId).HasColumnName("AccountId");
            e.Property(t => t.Amount).HasColumnName("Amount").HasPrecision(18, 2);
            e.Property(t => t.Currency).HasColumnName("Currency").HasMaxLength(3);
            e.Property(t => t.Type).HasColumnName("Type").HasMaxLength(20);
            e.Property(t => t.Status).HasColumnName("Status").HasMaxLength(20);
            e.Property(t => t.TransactedAt).HasColumnName("TransactedAt").HasConversion(utcConverter);
            e.Property(t => t.ReceivedAt).HasColumnName("ReceivedAt").HasConversion(utcConverter);
            e.Property(t => t.Metadata).HasColumnName("Metadata");

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

            e.Property(a => a.AccountId).HasColumnName("AccountId");
            e.Property(a => a.TotalCredits).HasColumnName("TotalCredits").HasPrecision(18, 2);
            e.Property(a => a.TotalDebits).HasColumnName("TotalDebits").HasPrecision(18, 2);
            e.Property(a => a.TransactionCount).HasColumnName("TransactionCount");
            e.Property(a => a.LastUpdated).HasColumnName("LastUpdated").HasConversion(utcConverter);

            e.Ignore(a => a.RunningBalance);
        });
    }
}
