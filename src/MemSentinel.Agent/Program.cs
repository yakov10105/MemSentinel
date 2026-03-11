using Log = MemSentinel.Agent.Logging.Log;
using MemSentinel.Agent;
using MemSentinel.Agent.Infrastructure;
using MemSentinel.Contracts.Options;
using MemSentinel.Core.Collectors;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog(lc => lc
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithProperty("PodName", Environment.GetEnvironmentVariable("POD_NAME") ?? "local")
    .Enrich.WithProperty("Namespace", Environment.GetEnvironmentVariable("POD_NAMESPACE") ?? "local")
    .WriteTo.Console(new CompactJsonFormatter()));

builder.Services.Configure<SentinelOptions>(
    builder.Configuration.GetSection(SentinelOptions.SectionName));

builder.Services.AddMemoryProvider();

builder.Services.AddHostedService<Worker>();

var app = builder.Build();

var appLogger = app.Services.GetRequiredService<ILogger<Program>>();
TaskScheduler.UnobservedTaskException += (_, e) =>
{
    Log.UnobservedTaskException(appLogger, e.Exception);
    e.SetObserved();
};

app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "1.0.0" }));

app.MapGet("/ready", async (IDiagnosticPortLocator locator, CancellationToken ct) =>
{
    var socketPath = await locator.TryFindSocketPathAsync(ct);
    return socketPath is not null
        ? Results.Ok(new { status = "ready", socketPath })
        : Results.Json(new { status = "not_ready", reason = "diagnostic_port_not_found" }, statusCode: 503);
});

app.Run();
