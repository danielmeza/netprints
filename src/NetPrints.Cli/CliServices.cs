using System;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetPrints.Cli.Infrastructure;
using NetPrints.Projects;
using NetPrints.Workspace;
using Spectre.Console;

namespace NetPrints.Cli;

/// <summary>The production service registrations of the tool; tests build their own collection with fakes.</summary>
internal static class CliServices
{
    private const string NoColorVariable = "NO_COLOR";
    private const string DevelopmentSdkVersion = "1.0.0-dev";

    /// <summary>Creates the service collection of the real process: its console, environment, process runner and MSBuild-backed project system.</summary>
    /// <returns>The registrations <see cref="CliApplication.RunAsync"/> completes with logging and the commands.</returns>
    public static IServiceCollection CreateDefault()
    {
        CliEnvironment environment = CliEnvironment.FromProcess();
        var services = new ServiceCollection();
        services.AddSingleton(environment);
        services.AddSingleton(CreateConsole(Console.Out, Console.IsOutputRedirected, environment));
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IMsBuildRegistration, MsBuildRegistrationAdapter>();
        services.AddSingleton(provider => new Lazy<IProjectSystem>(() => CreateProjectSystem(provider)));
        return services;
    }

    /// <summary>Creates the console results are written to.</summary>
    /// <param name="writer">The output writer.</param>
    /// <param name="isRedirected">Whether the output goes to a file or pipe.</param>
    /// <param name="environment">Supplies <c>NO_COLOR</c>.</param>
    /// <returns>A console that emits no ANSI escape when redirected or when <c>NO_COLOR</c> is set.</returns>
    public static IAnsiConsole CreateConsole(TextWriter writer, bool isRedirected, CliEnvironment environment)
    {
        AnsiConsoleSettings settings = ConsoleSettings(isRedirected, environment);
        settings.Out = new AnsiConsoleOutput(writer);
        return AnsiConsole.Create(settings);
    }

    /// <summary>Chooses the ANSI and color support for the given output.</summary>
    /// <param name="isRedirected">Whether the output goes to a file or pipe.</param>
    /// <param name="environment">Supplies <c>NO_COLOR</c>.</param>
    /// <returns>Plain-text settings when redirected or <c>NO_COLOR</c> is set, otherwise detection.</returns>
    public static AnsiConsoleSettings ConsoleSettings(bool isRedirected, CliEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        bool plain = isRedirected || !string.IsNullOrEmpty(environment.GetVariable(NoColorVariable));
        return new AnsiConsoleSettings
        {
            Ansi = plain ? AnsiSupport.No : AnsiSupport.Detect,
            ColorSystem = plain ? ColorSystemSupport.NoColors : ColorSystemSupport.Detect,
        };
    }

    // GenerateOnLoad is off so `generate --check` sees the generated files as they are; `build` and `run` generate through dotnet build.
    // Kept out of line: loading MsBuildProjectSystem loads Microsoft.Build, which must wait for the SDK registration.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IProjectSystem CreateProjectSystem(IServiceProvider provider) =>
        new MsBuildProjectSystem(
            new ProjectSystemOptions([], DevelopmentSdkVersion, GenerateOnLoad: false),
            provider.GetRequiredService<IProcessRunner>(),
            provider.GetRequiredService<ILoggerFactory>().CreateLogger<MsBuildProjectSystem>());
}
