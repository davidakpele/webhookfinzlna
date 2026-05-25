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

        // Ensure all DateTime values are stored/read as UTC
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        modelBuilder.Entity<Transaction>(e =>
        {
            e.ToTable("transactions");
            e.HasKey(t => t.Id);

            e.HasIndex(t => t.ExternalRef)
             .IsUnique()
             .HasDatabaseName("idx_transactions_external_ref");

            e.HasIndex(t => t.AccountId)
             .HasDatabaseName("idx_transactions_account_id");

            e.Property(t => t.Amount).HasPrecision(18, 2);
            e.Property(t => t.Currency).HasMaxLength(3);
            e.Property(t => t.Type).HasMaxLength(20);
            e.Property(t => t.Status).HasMaxLength(20);
            e.Property(t => t.TransactedAt).HasConversion(utcConverter);
            e.Property(t => t.ReceivedAt).HasConversion(utcConverter);
        });

        modelBuilder.Entity<AccountSummary>(e =>
        {
            e.ToTable("account_summaries");
            e.HasKey(a => a.AccountId);

            e.Property(a => a.TotalCredits).HasPrecision(18, 2);
            e.Property(a => a.TotalDebits).HasPrecision(18, 2);
            e.Property(a => a.LastUpdated).HasConversion(utcConverter);

            // Computed in-memory — not a DB column
            e.Ignore(a => a.RunningBalance);
        });
    }
}
