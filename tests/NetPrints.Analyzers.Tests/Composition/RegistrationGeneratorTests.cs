using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace NetPrints.Analyzers.Tests.Composition;

public class RegistrationGeneratorTests
{
    private const string RegistrationHint = "AddEditorServices.g.cs";

    private const string RegistrationsStep = "Registrations";

    private const string SizeSampleServices = "SizeSample";

    private const int SizeSampleCount = 240;

    private const int SizeBoundLines = 600;

    private const string Sample = """
        using System;
        using Microsoft.Extensions.Logging;
        using NetPrints.Composition;

        namespace Sample;

        public interface IClock { }

        public interface IStore { }

        public interface ISession { }

        public interface IOptions { }

        [Singleton<IClock>]
        public sealed class Clock : IClock { }

        [Singleton<IStore>]
        public sealed class Store(IClock clock, ILogger<Store> logger) : IStore { }

        [SessionScoped<ISession>]
        public sealed class Session(IStore store, int retries = 3) : ISession { }

        [Singleton<IOptions>(Factory = nameof(Options.Create))]
        public sealed class Options : IOptions
        {
            public static IOptions Create(IClock clock) => new Options();
        }
        """;

    private const string RuntimeSample = """
        using System;
        using System.Collections.Generic;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.Logging;
        using Microsoft.Extensions.Logging.Abstractions;
        using NetPrints.Composition;

        namespace Sample;

        public static class Trace
        {
            public static readonly List<string> Events = [];
        }

        public interface IHostValue { string Value { get; } }

        public sealed class HostValue(string value) : IHostValue { public string Value => value; }

        public sealed class RecordingFactory : ILoggerFactory
        {
            public void AddProvider(ILoggerProvider provider) { }

            public ILogger CreateLogger(string categoryName)
            {
                Trace.Events.Add("logger:" + categoryName);
                return NullLogger.Instance;
            }

            public void Dispose() { }
        }

        public interface IA { }

        public interface IB { }

        [Singleton<IA>]
        [Singleton<IB>]
        public sealed class Both : IA, IB { }

        public interface IHeld { }

        [Singleton<IHeld>]
        public sealed class Held : IHeld, IDisposable
        {
            public void Dispose() => Trace.Events.Add("held");
        }

        public interface IUsesLogger { IHostValue Host { get; } }

        [Singleton<IUsesLogger>]
        public sealed class UsesLogger(ILogger<UsesLogger> typed, ILogger plain, IHostValue host) : IUsesLogger
        {
            public IHostValue Host => host;
        }

        public interface IMade { string Origin { get; } }

        [Singleton<IMade>(Factory = nameof(Made.Create))]
        public sealed class Made : IMade
        {
            private Made(string origin) => Origin = origin;

            public string Origin { get; }

            public static IMade Create(IHostValue host) => new Made("factory:" + host.Value);
        }

        public interface IScoped { }

        [SessionScoped<IScoped>]
        public sealed class Scoped : IScoped { }

        public static class Probe
        {
            public static string Run()
            {
                Trace.Events.Clear();
                ServiceCollection services = new();
                services.AddSingleton<ILoggerFactory>(new RecordingFactory());
                services.AddSingleton<IHostValue>(new HostValue("from-host"));
                services.AddEditorServices();
                ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
                IUsesLogger uses = provider.GetRequiredService<IUsesLogger>();
                bool same = ReferenceEquals(provider.GetRequiredService<IA>(), provider.GetRequiredService<IB>());
                IScoped one;
                IScoped again;
                IScoped two;
                using (IServiceScope first = provider.CreateScope())
                {
                    one = first.ServiceProvider.GetRequiredService<IScoped>();
                    again = first.ServiceProvider.GetRequiredService<IScoped>();
                }

                using (IServiceScope second = provider.CreateScope())
                {
                    two = second.ServiceProvider.GetRequiredService<IScoped>();
                }

                string made = provider.GetRequiredService<IMade>().Origin;
                provider.GetRequiredService<IHeld>();
                provider.Dispose();
                return $"host={uses.Host.Value};same={same};scopes={ReferenceEquals(one, again)}/{ReferenceEquals(one, two)};made={made};events={string.Join(',', Trace.Events)}";
            }
        }
        """;

    private const string ScopeFactorySample = Sample + """


        [ScopeFactory]
        public sealed partial class ContextFactory
        {
            private readonly IServiceProvider services;

            public ContextFactory(IServiceProvider services) => this.services = services;

            public partial IStore CreateStore();

            public partial Widget CreateWidget(string title);
        }

        public sealed class Widget(string title, IClock clock)
        {
            public string Title => title;

            public IClock Clock => clock;
        }
        """;

    [Fact]
    public void ARecordIsRegisteredLikeAClass()
    {
        GeneratorRun run = GeneratorHarness.Run("""
            using NetPrints.Composition;

            namespace Sample;

            public interface IPoint { }

            [Singleton<IPoint>]
            public sealed record Point : IPoint;
            """);

        Assert.Contains("AddSingleton<global::Sample.IPoint>(static sp => new global::Sample.Point())", run.Source(RegistrationHint), StringComparison.Ordinal);
    }

    [Fact]
    public void TheRegistrationsAreFactoryLambdasThatCallConstructors()
    {
        GeneratorRun run = GeneratorHarness.Run(Sample);

        Assert.Equal(
            """
            // <auto-generated/>
            #nullable enable
            using global::Microsoft.Extensions.DependencyInjection;

            namespace NetPrints.Composition
            {
                internal static class GeneratedServiceRegistration
                {
                    internal static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddEditorServices(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                    {
                        services.AddSingleton<global::Sample.IClock>(static sp => new global::Sample.Clock());
                        services.AddSingleton<global::Sample.IOptions>(static sp => global::Sample.Options.Create(sp.GetRequiredService<global::Sample.IClock>()));
                        services.AddScoped<global::Sample.ISession>(static sp => new global::Sample.Session(sp.GetRequiredService<global::Sample.IStore>(), 3));
                        services.AddSingleton<global::Sample.IStore>(static sp => new global::Sample.Store(sp.GetRequiredService<global::Sample.IClock>(), global::Microsoft.Extensions.Logging.LoggerFactoryExtensions.CreateLogger<global::Sample.Store>(sp.GetRequiredService<global::Microsoft.Extensions.Logging.ILoggerFactory>())));
                        return services;
                    }
                }
            }

            """.Replace("\r\n", "\n", StringComparison.Ordinal),
            run.Source(RegistrationHint));
    }

    [Fact]
    public void TheOutputCompilesWithoutErrors()
    {
        GeneratorRun run = GeneratorHarness.Run(Sample, RuntimeSample);

        Assert.Empty(run.CompileErrors());
        Assert.Empty(run.Diagnostics);
    }

    [Fact]
    public void NothingIsEmittedWithoutRegistrations()
    {
        GeneratorRun run = GeneratorHarness.Run("namespace Sample { public class Empty { } }");

        Assert.DoesNotContain(RegistrationHint, run.Sources.Keys);
    }

    [Fact]
    public void TheGeneratedRegistrationsResolveScopeShareAndDisposeOnce()
    {
        GeneratorRun run = GeneratorHarness.Run(RuntimeSample);
        Assembly assembly = GeneratorHarness.EmitAndLoad(run.Output);

        string result = (string)(assembly.GetType("Sample.Probe")?.GetMethod("Run")?.Invoke(null, null) ?? string.Empty);

        Assert.Equal("host=from-host;same=True;scopes=True/False;made=factory:from-host;events=logger:Sample.UsesLogger,logger:Sample.UsesLogger,held", result);
    }

    [Fact]
    public void CreateMethodsOfAScopeFactoryAreImplemented()
    {
        GeneratorRun run = GeneratorHarness.Run(ScopeFactorySample);

        Assert.Empty(run.CompileErrors());
        Assert.Equal(
            """
            // <auto-generated/>
            #nullable enable
            using global::Microsoft.Extensions.DependencyInjection;

            namespace Sample
            {
                partial class ContextFactory
                {
                    public partial global::Sample.IStore CreateStore()
                    {
                        return new global::Sample.Store(services.GetRequiredService<global::Sample.IClock>(), global::Microsoft.Extensions.Logging.LoggerFactoryExtensions.CreateLogger<global::Sample.Store>(services.GetRequiredService<global::Microsoft.Extensions.Logging.ILoggerFactory>()));
                    }

                    public partial global::Sample.Widget CreateWidget(string title)
                    {
                        return new global::Sample.Widget(title, services.GetRequiredService<global::Sample.IClock>());
                    }
                }
            }

            """.Replace("\r\n", "\n", StringComparison.Ordinal),
            run.Source("ScopeFactory.Sample.ContextFactory.g.cs"));
    }

    [Fact]
    public void AnUnrelatedChangeKeepsTheRegistrationsStepCached()
    {
        const string Unrelated = "namespace Other { public class Unrelated { } }";
        CSharpCompilation compilation = GeneratorHarness.Compile(Sample, Unrelated);
        GeneratorDriver driver = GeneratorHarness.CreateDriver(trackSteps: true).RunGenerators(compilation, TestContext.Current.CancellationToken);
        Assert.All(StepReasons(driver), reason => Assert.Equal(IncrementalStepRunReason.New, reason));

        SyntaxTree changed = CSharpSyntaxTree.ParseText("namespace Other { public class Unrelated { public int X; } }", path: "Sample1.cs", cancellationToken: TestContext.Current.CancellationToken);
        CSharpCompilation edited = compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Last(), changed);
        driver = driver.RunGenerators(edited, TestContext.Current.CancellationToken);

        Assert.All(StepReasons(driver), reason => Assert.True(reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged, reason.ToString()));
    }

    [Fact]
    public void AChangeToARegisteredTypeRerunsTheRegistrationsStep()
    {
        CSharpCompilation compilation = GeneratorHarness.Compile(Sample);
        GeneratorDriver driver = GeneratorHarness.CreateDriver(trackSteps: true).RunGenerators(compilation, TestContext.Current.CancellationToken);

        SyntaxTree changed = CSharpSyntaxTree.ParseText(Sample.Replace("int retries = 3", "int retries = 4", StringComparison.Ordinal), path: "Sample0.cs", cancellationToken: TestContext.Current.CancellationToken);
        driver = driver.RunGenerators(compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.First(), changed), TestContext.Current.CancellationToken);

        Assert.Contains(StepReasons(driver), reason => reason == IncrementalStepRunReason.Modified);
        Assert.Contains("new global::Sample.Session(sp.GetRequiredService<global::Sample.IStore>(), 4)", GeneratorHarness.Sources(driver)[RegistrationHint], StringComparison.Ordinal);
    }

    [Fact]
    public void TheGeneratedFileStaysWithinTheSizeBound()
    {
        StringBuilder source = new("using NetPrints.Composition;\nnamespace Sample;\n");
        for (int index = 0; index < SizeSampleCount; index++)
        {
            string dependency = index == 0 ? string.Empty : $"IS{index - 1} previous";
            source.Append($"public interface IS{index} {{ }}\n[Singleton<IS{index}>]\npublic sealed class C{index}({dependency}) : IS{index} {{ }}\n");
        }

        GeneratorRun run = GeneratorHarness.Run(source.ToString());

        int lines = run.Source(RegistrationHint).Split('\n').Length;
        TestContext.Current.SendDiagnosticMessage($"{SizeSampleServices}: {SizeSampleCount} registrations, {lines} lines");
        Assert.Empty(run.CompileErrors());
        Assert.InRange(lines, SizeSampleCount, SizeBoundLines);
    }

    private static ImmutableArray<IncrementalStepRunReason> StepReasons(GeneratorDriver driver) =>
    [
        .. driver.GetRunResult().Results
            .Where(result => result.TrackedSteps.ContainsKey(RegistrationsStep))
            .SelectMany(result => result.TrackedSteps[RegistrationsStep])
            .SelectMany(step => step.Outputs)
            .Select(output => output.Reason),
    ];
}
