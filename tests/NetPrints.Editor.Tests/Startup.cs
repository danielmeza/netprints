using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Tests.Hosting;

namespace NetPrints.Editor.Tests;

/// <summary>
/// Xunit.DependencyInjection composition for the non-UI editor tests.
/// </summary>
public sealed class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Loading the runtime assembly set takes seconds, so one loaded host is shared by all tests.
        // Its own reload is async; the README's "Initializing data on startup" says to load an async
        // singleton through an IHostedService, not a blocking GetAwaiter().GetResult() in the factory.
        services.AddSingleton(_ =>
            new ReflectionHost(new InlineDispatcher(), TestExtensions.CreateBuiltIn(), NullLogger<ReflectionHost>.Instance));
        services.AddHostedService<ReflectionHostWarmup>();

        // Each test sees the shared host through its own scope, so what a test leaves subscribed goes with it.
        services.AddScoped<IReflectionHost>(provider => new ScopedReflectionHost(provider.GetRequiredService<ReflectionHost>()));

        services.AddTransient<TestEditor>();
    }

    /// <summary>Loads the shared <see cref="ReflectionHost"/> once, before any test runs.</summary>
    private sealed class ReflectionHostWarmup(ReflectionHost host) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            var project = Project.FromSnapshot(TestSnapshots.WithRuntimeAssemblies("Shared", "Shared"));
            return host.ReloadAsync(project, cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
