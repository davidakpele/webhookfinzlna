using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using WebhooksAPI.Configurations;
using WebhooksAPI.Data.Repositories;
using WebhooksAPI.Data.Services;

var builder = WebApplication.CreateBuilder(args);

// ── PostgreSQL ────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));

// ── Redis ─────────────────────────────────────────────────────────────────────
// abortConnect=false → app starts even if Redis is offline; idempotency degrades to DB-only
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var connStr = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    var options = ConfigurationOptions.Parse(connStr);
    options.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(options);
});

// ── App services ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<IIdempotencyService, IdempotencyService>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<ITransactionService, TransactionService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "Webhooks Transaction API",
        Version     = "v1",
        Description = """
            Ingests transaction webhooks from external providers.

            **Idempotency:** Every request is keyed on `externalRef`.
            Duplicate submissions return HTTP 200 with the original record.
            New submissions return HTTP 201 with the stored record + derived account summary.

            **Derived computation:** After each new *completed* transaction,
            the account's `AccountSummary` (total credits, debits, running balance) is recomputed.
            """
    });

    // Pull in XML doc comments from the compiled assembly
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// ── Auto-create tables on startup ────────────────────────────────────────────
// EnsureCreated creates the DB + all tables if they don't exist yet.
// It's a no-op when the schema is already in place.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// ── Swagger — always on (restrict in prod via env/config if needed) ───────────
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Webhooks API v1");
    c.RoutePrefix = string.Empty; // Serve Swagger UI at root "/"
    c.DisplayRequestDuration();
    c.EnableDeepLinking();
});

app.UseHttpsRedirection();
app.MapControllers();
app.Run();

// Expose for integration tests
public partial class Program { }
