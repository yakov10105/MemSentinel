using MemSentinel.Agent;
using MemSentinel.Agent.Logging;
using MemSentinel.Contracts.Options;
using MemSentinel.Core.Providers;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

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
