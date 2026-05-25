using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using WebhooksAPI.Configurations;
using WebhooksAPI.Data.Repositories;
using WebhooksAPI.Data.Services;

var builder = WebApplication.CreateBuilder(args);

// ── PostgreSQL ────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));

// ── Redis ─────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

// ── App services ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<IIdempotencyService, IdempotencyService>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<ITransactionService, TransactionService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ── Auto-migrate on startup ───────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();

// Expose Program for integration tests
public partial class Program { }
