using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetPrints.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetPrints.Cli;

/// <summary>The <c>netprints</c> command line: argument pre-parse, Spectre configuration and the exit-code contract.</summary>
internal static class CliApplication
{
    private const string ApplicationName = "NetPrints.Cli";
    private const string VerboseFlag = "--verbose";
    private const string Separator = "--";
    private const string P1FlagsMessage =
        "The -p/--project-path and -r/--run options were replaced: use 'netprints build <project>' or 'netprints run <project>'.";

    private static readonly string[] P1Flags = ["-p", "--project-path", "-r", "--run"];

    /// <summary>Runs the tool.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="services">The registrations commands resolve from; logging is added here.</param>
    /// <param name="cancellationToken">Cancels the running command.</param>
    /// <returns>The process exit code (<see cref="ExitCodes"/>).</returns>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, IServiceCollection services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(services);

        CliEnvironment environment;
        IAnsiConsole console;
        using (ServiceProvider probe = services.BuildServiceProvider())
        {
            environment = probe.GetRequiredService<CliEnvironment>();
            console = probe.GetRequiredService<IAnsiConsole>();
        }

        if (P1FlagPresent(args))
        {
            await environment.Error.WriteLineAsync(P1FlagsMessage).ConfigureAwait(false);
            return ExitCodes.Usage;
        }

        IReadOnlyList<string> normalized = MoveLeadingVerbose(args);
        bool verbose = normalized.TakeWhile(arg => arg != Separator).Contains(VerboseFlag);

        services.AddLogging(builder => builder
            .AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace)
            .SetMinimumLevel(verbose ? LogLevel.Information : LogLevel.Warning));

        using var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.SetApplicationName("netprints");
            config.UseStrictParsing();
            config.SetApplicationVersion(ApplicationName + " " + InformationalVersion());
            config.ConfigureConsole(console);
            config.SetExceptionHandler((exception, _) => HandleException(exception, environment.Error, verbose));
            ConfigureCommands(config);
        });

        return await app.RunAsync(normalized, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Registers every command of <see cref="CliCommandCatalog"/>.</summary>
    /// <param name="config">The Spectre configurator.</param>
    public static void ConfigureCommands(IConfigurator config)
    {
        ArgumentNullException.ThrowIfNull(config);
        foreach (CliCommand command in CliCommandCatalog.All)
        {
            command.Register(config);
        }
    }

    private static bool P1FlagPresent(IReadOnlyList<string> args)
    {
        foreach (string arg in args)
        {
            if (arg == Separator || !arg.StartsWith('-'))
            {
                return false;
            }

            if (P1Flags.Contains(arg, StringComparer.Ordinal) || arg.StartsWith("--project-path=", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> MoveLeadingVerbose(IReadOnlyList<string> args)
    {
        int command = args.ToList().FindIndex(arg => !arg.StartsWith('-'));
        if (command <= 0 || !args.Take(command).Contains(VerboseFlag))
        {
            return args;
        }

        var moved = new List<string>(args.Where((_, index) => index >= command || args[index] != VerboseFlag));
        moved.Insert(moved.IndexOf(args[command]) + 1, VerboseFlag);
        return moved;
    }

    private static int HandleException(Exception exception, TextWriter error, bool verbose)
    {
        if (exception is CommandParseException or CommandRuntimeException)
        {
            error.WriteLine(exception.Message);
            return ExitCodes.Usage;
        }

        error.WriteLine(verbose ? exception.ToString() : "Internal error: " + exception.Message);
        return ExitCodes.InternalError;
    }

    private static string InformationalVersion() =>
        typeof(CliApplication).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(CliApplication).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";
}
