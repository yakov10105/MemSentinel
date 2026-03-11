using MemSentinel.Contracts.Options;
using MemSentinel.Core.Collectors;
using MemSentinel.Core.Providers;
using Microsoft.Extensions.Options;

namespace MemSentinel.Agent.Infrastructure;

internal static class CoreExtensions
{
    internal static IServiceCollection AddMemoryProvider(this IServiceCollection services)
    {
        services.AddSingleton<IMemoryProvider>(sp =>
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

        services.AddSingleton<IDiagnosticPortLocator>(
            OperatingSystem.IsLinux()
                ? new UnixDiagnosticPortLocator()
                : new MockDiagnosticPortLocator());

        return services;
    }
}
