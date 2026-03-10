using MemSentinel.Agent;
using Log = MemSentinel.Agent.Logging.Log;
using MemSentinel.Contracts.Options;
using MemSentinel.Core.Providers;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Formatting.Compact;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(lc => lc
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithProperty("PodName", Environment.GetEnvironmentVariable("POD_NAME") ?? "local")
    .Enrich.WithProperty("Namespace", Environment.GetEnvironmentVariable("POD_NAMESPACE") ?? "local")
    .WriteTo.Console(new CompactJsonFormatter()));

builder.Services.Configure<SentinelOptions>(
    builder.Configuration.GetSection(SentinelOptions.SectionName));

builder.Services.AddSingleton<IMemoryProvider>(sp =>
{
    var options = sp.GetRequiredService<IOptions<SentinelOptions>>().Value;

    if (OperatingSystem.IsLinux())
    {
        var target = System.Diagnostics.Process
            .GetProcessesByName(options.TargetProcessName)
            .FirstOrDefault();

        var pid = target?.Id ?? System.Diagnostics.Process.GetCurrentProcess().Id;
        return new LinuxMemoryProvider(pid);
    }

    return new MockMemoryProvider();
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

var appLogger = host.Services.GetRequiredService<ILogger<Program>>();
TaskScheduler.UnobservedTaskException += (_, e) =>
{
    Log.UnobservedTaskException(appLogger, e.Exception);
    e.SetObserved();
};

host.Run();
