using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetPrints.Core;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Settings;
using Xunit;
using static NetPrints.Tests.Extensibility.ExtensionTestSupport;

namespace NetPrints.Tests.Extensibility;

public interface IGreeter
{
    string Greet();
}

public sealed class Greeter : IGreeter
{
    public string Greet() => "hello";
}

public sealed class OtherService;

public class DisposeProbe(List<string> log, string name, bool throws) : IDisposable
{
    public void Dispose()
    {
        log.Add(name);
        if (throws)
        {
            throw new InvalidOperationException(name);
        }
    }
}

public sealed class SessionProbe(List<string> log, string name, bool throws) : DisposeProbe(log, name, throws);

/// <summary>FR-024, NPXE0008, research R15: one provider per extension, the host exports allow-list and the seam filter.</summary>
public class ExtensionServicesTests
{
    private static readonly ExtensionSettingsDescriptor<SampleSettings> Section =
        new("test.ext", SampleSettingsContext.Default.SampleSettings, new SampleSettings("default", 1));

    [Fact]
    public async Task AnExtensionResolvesItsOwnRegistrationsAndTheAllowList()
    {
        var extension = InProcess("test.ext", builder =>
        {
            builder.Services.AddSingleton<IGreeter, Greeter>();
            builder.AddSettings(Section);
        });

        await using ExtensionRegistry registry = Load(Options([BuiltInExtension.InProcessEntry, extension]));
        IServiceProvider services = registry.ServicesOf("test.ext");

        Assert.Equal("hello", services.GetRequiredService<IGreeter>().Greet());
        Assert.NotNull(services.GetService<ILoggerFactory>());
        Assert.NotNull(services.GetService<ILogger<Greeter>>());
        Assert.NotNull(services.GetService<TimeProvider>());
        Assert.Equal(new SampleSettings("default", 1), services.GetRequiredService<IOptionsMonitor<SampleSettings>>().CurrentValue);
    }

    [Fact]
    public async Task AnExtensionDoesNotSeeOtherServices()
    {
        var first = InProcess("test.first", builder => builder.Services.AddSingleton<IGreeter, Greeter>());
        var second = InProcess("test.second", builder => builder.Services.AddSingleton<OtherService>());

        await using ExtensionRegistry registry = Load(Options([BuiltInExtension.InProcessEntry, first, second]));

        Assert.Null(registry.ServicesOf("test.first").GetService<OtherService>());
        Assert.Null(registry.ServicesOf("test.second").GetService<IGreeter>());
        Assert.Null(registry.ServicesOf("test.unknown").GetService<IGreeter>());
        Assert.Null(registry.ServicesOf("test.first").GetService<IOptionsMonitor<SampleSettings>>());
    }

    [Fact]
    public async Task RegistrationsThatReplaceOrConfigureHostServicesAreRejectedAndTheExtensionStaysLoaded()
    {
        var extension = InProcess("test.ext", builder =>
        {
            builder.Services.AddSingleton<IGreeter, Greeter>();
            builder.Services.AddSingleton<IList<NodeGraph>>(new List<NodeGraph>());
            builder.Services.AddSingleton<IConfigureOptions<NodeGraph>>(new ConfigureOptions<NodeGraph>(_ => { }));
            builder.Services.AddSingleton<IPostConfigureOptions<NodeGraph>>(new PostConfigureOptions<NodeGraph>(null, _ => { }));
            builder.Services.AddSingleton<IValidateOptions<NodeGraph>>(new ValidateOptions<NodeGraph>(null, _ => true, "no"));
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<IConfigureOptions<SampleSettings>>(new ConfigureOptions<SampleSettings>(_ => { }));
        });

        await using ExtensionRegistry registry = Load(Options([BuiltInExtension.InProcessEntry, extension]));
        IServiceProvider services = registry.ServicesOf("test.ext");

        Assert.Contains(registry.Results, result => result is ExtensionLoadResult.Loaded loaded && loaded.Id == "test.ext");
        Assert.Equal(5, registry.Issues.Count(issue => issue.ExtensionId == "test.ext" && issue.Code == ExtensionDiagnosticCodes.ContributionRejected));
        Assert.NotNull(services.GetService<IGreeter>());
        Assert.Null(services.GetService<IList<NodeGraph>>());
        Assert.Null(services.GetService<IConfigureOptions<NodeGraph>>());
        Assert.NotNull(services.GetService<IConfigureOptions<SampleSettings>>());
    }

    [Fact]
    public void TheAllowListIsPinned()
    {
        Assert.Equal([typeof(ILoggerFactory), typeof(ILogger<>), typeof(TimeProvider), typeof(IOptionsMonitor<>)], HostExports.AllowedServiceTypes);
    }

    [Fact]
    public async Task DisposalRunsSessionsThenProvidersAndOneThrowingDisposeDoesNotStopTheRest()
    {
        var log = new List<string>();
        var first = InProcess("test.first", builder =>
        {
            builder.Services.AddSingleton(_ => new DisposeProbe(log, "first-provider", true));
            builder.Services.AddScoped(_ => new SessionProbe(log, "first-session", true));
        });
        var second = InProcess("test.second", builder =>
        {
            builder.Services.AddSingleton(_ => new DisposeProbe(log, "second-provider", false));
            builder.Services.AddScoped(_ => new SessionProbe(log, "second-session", false));
        });

        ExtensionRegistry registry = Load(Options([BuiltInExtension.InProcessEntry, first, second]));
        IServiceScope firstSession = registry.OpenSession("test.first");
        IServiceScope secondSession = registry.OpenSession("test.second");
        firstSession.ServiceProvider.GetRequiredService<SessionProbe>();
        secondSession.ServiceProvider.GetRequiredService<SessionProbe>();
        registry.ServicesOf("test.first").GetRequiredService<DisposeProbe>();
        registry.ServicesOf("test.second").GetRequiredService<DisposeProbe>();

        await registry.DisposeAsync();

        Assert.Equal(["first-session", "second-session"], log.Take(2).Order());
        Assert.Equal(["first-provider", "second-provider"], log.Skip(2).Order());
    }
}
