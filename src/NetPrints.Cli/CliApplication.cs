using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    private const string VersionFlag = "--version";
    private const string Separator = "--";
    private const string P1FlagsMessage =
        "The -p/--project-path and -r/--run options were replaced: use 'netprints build <project>' or 'netprints run <project>'.";

    private static readonly string[] P1Flags = ["-p", "--project-path", "-r", "--run"];

    /// <summary>Runs the tool.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="services">The registrations commands resolve from; logging is added here.</param>
    /// <param name="cancellationToken">Cancels the running command.</param>
    /// <returns>The process exit code (<see cref="ExitCodes"/>).</returns>
    public static Task<int> RunAsync(IReadOnlyList<string> args, IServiceCollection services, CancellationToken cancellationToken) =>
        RunAsync(args, services, CliCommandCatalog.All, cancellationToken);

    /// <summary>Runs the tool with an explicit command list.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="services">The registrations commands resolve from; logging is added here.</param>
    /// <param name="commands">The commands to register.</param>
    /// <param name="cancellationToken">Cancels the running command.</param>
    /// <returns>The process exit code (<see cref="ExitCodes"/>).</returns>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, IServiceCollection services, IReadOnlyList<CliCommand> commands, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new ToolVersion(InformationalVersion()));
        CliEnvironment environment;
        IAnsiConsole console;
        ToolVersion tool;
        await using (ServiceProvider probe = services.BuildServiceProvider())
        {
            environment = probe.GetRequiredService<CliEnvironment>();
            console = probe.GetRequiredService<IAnsiConsole>();
            tool = probe.GetRequiredService<ToolVersion>();
        }

        // Everything after the first separator belongs to the user's program; Spectre never sees it (it would drop or reject valid values).
        int separator = args.ToList().IndexOf(Separator);
        IReadOnlyList<string> own = separator < 0 ? args : [.. args.Take(separator)];
        services.AddSingleton(new ForwardedArguments(separator < 0 ? [] : [.. args.Skip(separator + 1)]));

        if (P1FlagPresent(own))
        {
            await environment.Error.WriteLineAsync(P1FlagsMessage).ConfigureAwait(false);
            return ExitCodes.Usage;
        }

        IReadOnlyList<string> normalized = MoveLeadingVerbose(own);
        bool verbose = own.Contains(VerboseFlag);

        if (normalized is [VersionFlag])
        {
            console.WriteLineRaw(ApplicationName + " " + tool.Value);
            return ExitCodes.Success;
        }

        services.AddLogging(builder => builder
            .AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace)
            .SetMinimumLevel(verbose ? LogLevel.Information : LogLevel.Warning));

        await using var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.SetApplicationName("netprints");
            config.UseStrictParsing();
            config.SetApplicationVersion(ApplicationName + " " + tool.Value);
            config.ConfigureConsole(console);
            config.SetExceptionHandler((exception, _) => HandleException(exception, environment.Error, verbose, cancellationToken));
            ConfigureCommands(config, commands);
        });

        return await app.RunAsync(normalized, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Registers every command of <see cref="CliCommandCatalog"/>.</summary>
    /// <param name="config">The Spectre configurator.</param>
    public static void ConfigureCommands(IConfigurator config) => ConfigureCommands(config, CliCommandCatalog.All);

    private static void ConfigureCommands(IConfigurator config, IReadOnlyList<CliCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(config);
        foreach (CliCommand command in commands)
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
        if (command < 0)
        {
            // No command: --verbose has no effect on help or version, and the root command has no such option.
            return [.. args.Where(arg => arg != VerboseFlag)];
        }

        if (command == 0 || !args.Take(command).Contains(VerboseFlag))
        {
            return args;
        }

        var moved = new List<string>(args.Where((_, index) => index >= command || args[index] != VerboseFlag));
        moved.Insert(moved.IndexOf(args[command]) + 1, VerboseFlag);
        return moved;
    }

    // Spectre raises CommandRuntimeException for usage problems (conversion, validation, missing values) and for DI and command-creation
    // faults alike, with no structural difference; the faults are recognised by the fixed prefix of their messages.
    private static readonly string[] InternalRuntimeMessages =
    [
        "Could not resolve type",
        "Could not create",
        "Could not find converter",
        "Could not get settings type",
    ];

    private static int HandleException(Exception exception, TextWriter error, bool verbose, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return ExitCodes.Canceled;
        }

        if (exception is CommandParseException || (exception is CommandRuntimeException && !IsInternalRuntimeFault(exception.Message)))
        {
            error.WriteLine(exception.Message);
            return ExitCodes.Usage;
        }

        error.WriteLine(verbose ? exception.ToString() : "Internal error: " + exception.Message);
        return ExitCodes.InternalError;
    }

    private static bool IsInternalRuntimeFault(string message) =>
        InternalRuntimeMessages.Any(prefix => message.StartsWith(prefix, StringComparison.Ordinal));

    private static string InformationalVersion() =>
        typeof(CliApplication).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(CliApplication).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";
}
