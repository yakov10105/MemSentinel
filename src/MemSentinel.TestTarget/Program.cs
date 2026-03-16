using MemSentinel.TestTarget;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "MemSentinel TestTarget";
    options.DefaultHttpClient = new(ScalarTarget.Shell, ScalarClient.Curl);
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("Health")
    .WithSummary("Health check");

app.MapPost("/leak/managed", () =>
{
    var (arrays, strings, bytes) = LeakStore.AddManaged();
    return Results.Ok(new
    {
        status = "leaked",
        totalArrays = arrays,
        totalStrings = strings,
        estimatedBytes = bytes,
        estimatedMb = Math.Round(bytes / 1_048_576.0, 2)
    });
})
.WithName("LeakManaged")
.WithSummary("Allocate managed memory")
.WithDescription("Adds 500 × 2KB byte arrays, 200 × 1KB strings, and 10 × 90KB arrays (LOH) to static collections. Call repeatedly to grow Gen2/LOH and trigger MemSentinel.");

app.MapPost("/leak/unmanaged", () =>
{
    var (handles, clients) = LeakStore.AddUnmanaged();
    return Results.Ok(new
    {
        status = "leaked",
        totalPinnedHandles = handles,
        totalHttpClients = clients,
        estimatedPinnedMb = Math.Round(handles * 262_144.0 / 1_048_576.0, 2)
    });
})
.WithName("LeakUnmanaged")
.WithSummary("Allocate unmanaged memory")
.WithDescription("Pins 5 × 256KB GCHandle buffers and creates 10 leaked HttpClient instances per call. Increases native/unmanaged RSS without GC pressure.");

app.MapPost("/leak/reset", () =>
{
    LeakStore.Reset();
    return Results.Ok(new { status = "reset" });
})
.WithName("Reset")
.WithSummary("Release all leaked memory")
.WithDescription("Frees all GCHandles, disposes HttpClients, clears all static collections, and forces a blocking Gen2 GC. RSS should drop measurably within one MemSentinel poll cycle.");

app.Run();
