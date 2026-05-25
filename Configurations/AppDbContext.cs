using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WebhooksAPI.data.Models;

namespace WebhooksAPI.Configurations
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<AccountSummary> AccountSummary { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.ConfigureWarnings(w =>
                w.Ignore(RelationalEventId.PendingModelChangesWarning));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var utcConverter = new ValueConverter<DateTime, DateTime>(
                v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
            );

            var utcNullableConverter = new ValueConverter<DateTime?, DateTime?>(
                v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)) : v,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v
            );

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                        property.SetValueConverter(utcConverter);

                    if (property.ClrType == typeof(DateTime?))
                        property.SetValueConverter(utcNullableConverter);
                }
            }

            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.ToTable("transactions");
                entity.HasKey(t => t.Id);
                
                entity.HasIndex(t => t.ExternalRef)
                    .IsUnique()
                    .HasDatabaseName("idx_transactions_external_ref");

                entity.HasIndex(t => t.AccountId)
                    .HasDatabaseName("idx_transactions_account_id");

                entity.Property(t => t.Amount)
                    .HasPrecision(18, 2);

                entity.Property(t => t.Currency)
                    .HasMaxLength(3);

                entity.Property(t => t.Type)
                    .HasMaxLength(20);

                entity.Property(t => t.Status)
                    .HasMaxLength(20);
            });

            modelBuilder.Entity<AccountSummary>(entity =>
            {
                entity.ToTable("account_summaries");
                entity.HasKey(a => a.AccountId);

                entity.Property(a => a.TotalCredits)
                    .HasPrecision(18, 2);

                entity.Property(a => a.TotalDebits)
                    .HasPrecision(18, 2);

                entity.Ignore(a => a.RunningBalance);
            });
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.Properties.Any(p => p.Metadata.Name == "UpdatedOn"))
                    entry.Property("UpdatedOn").CurrentValue = DateTime.UtcNow;

                if (entry.State == EntityState.Added &&
                    entry.Properties.Any(p => p.Metadata.Name == "CreatedOn"))
                    entry.Property("CreatedOn").CurrentValue = DateTime.UtcNow;
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}