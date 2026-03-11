using MemSentinel.Contracts.Options;
using MemSentinel.Core.Analysis;
using MemSentinel.Core.Collectors;
using MemSentinel.Core.Providers;
using Microsoft.Extensions.Options;

namespace MemSentinel.Agent.Infrastructure;

internal static class CoreExtensions
{
    internal static IServiceCollection AddMemoryProvider(this IServiceCollection services)
    {
        services.AddSingleton<IProcessLocator>(
            OperatingSystem.IsLinux()
                ? new SystemProcessLocator()
                : new MockProcessLocator());

        services.AddSingleton<IDiagnosticPortLocator>(
            OperatingSystem.IsLinux()
                ? new UnixDiagnosticPortLocator()
                : new MockDiagnosticPortLocator());

        if (OperatingSystem.IsLinux())
            services.AddSingleton<IDotNetDiagnosticClient>(sp =>
            {
                var processName = sp.GetRequiredService<IOptions<SentinelOptions>>().Value.TargetProcessName;
                var locator = sp.GetRequiredService<IProcessLocator>();
                return new DotNetDiagnosticClient(locator, processName);
            });
        else
            services.AddSingleton<IDotNetDiagnosticClient, MockDotNetDiagnosticClient>();

        services.AddSingleton<IMemoryProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SentinelOptions>>().Value;

            if (OperatingSystem.IsLinux())
            {
                var locator = sp.GetRequiredService<IProcessLocator>();
                var info = locator.FindTargetAsync(options.TargetProcessName, CancellationToken.None)
                    .GetAwaiter().GetResult();
                var pid = info?.Pid ?? System.Diagnostics.Process.GetCurrentProcess().Id;
                return new LinuxMemoryProvider(pid);
            }

            return new MockMemoryProvider();
        });

        services.AddSingleton<MetricsBuffer>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<SentinelOptions>>().Value;
            return new MetricsBuffer(TimeSpan.FromMinutes(opts.MetricsWindowMinutes));
        });

        return services;
    }
}
