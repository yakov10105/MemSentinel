using MemSentinel.Agent;
using MemSentinel.Agent.Logging;
using MemSentinel.Core.Providers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IMemoryProvider>(_ =>
{
    if (OperatingSystem.IsLinux())
    {
        var targetName = builder.Configuration["Sentinel:TargetProcessName"] ?? "dotnet";
        var target = System.Diagnostics.Process
            .GetProcessesByName(targetName)
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
