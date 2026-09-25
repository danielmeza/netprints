using Microsoft.Extensions.DependencyInjection;
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
        services.AddSingleton<IReflectionHost>(_ =>
        {
            var host = new ReflectionHost(new InlineDispatcher());
            host.ReloadAsync(Project.CreateNew("Shared", "Shared")).GetAwaiter().GetResult();
            return host;
        });

        services.AddTransient<TestEditor>();
    }
}
