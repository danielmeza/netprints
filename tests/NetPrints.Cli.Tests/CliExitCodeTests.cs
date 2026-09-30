using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Cli.Infrastructure;
using NetPrints.Cli.Tests.Support;
using Spectre.Console;
using Spectre.Console.Cli;
using Xunit;

namespace NetPrints.Cli.Tests;

public sealed class CliExitCodeTests : IDisposable
{
    private const char Escape = '\u001b';

    private readonly List<string> _directories = [];

    public void Dispose()
    {
        foreach (string directory in _directories)
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private CliTestHost NewHostInTempDirectory()
    {
        string directory = Directory.CreateTempSubdirectory("np-cli-").FullName;
        _directories.Add(directory);
        return new CliTestHost(directory);
    }

    public static TheoryData<string> CommandNames => [.. CliCommandCatalog.All.Select(command => command.Name)];

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("--verbose", "--help")]
    [InlineData("--verbose", "-h")]
    public async Task TopLevelHelpExitsZero(params string[] args)
    {
        var host = new CliTestHost();

        Assert.Equal(ExitCodes.Success, await host.RunAsync(args));
        Assert.Contains("USAGE", host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerboseBeforeVersionStillPrintsTheVersion()
    {
        var host = new CliTestHost();

        Assert.Equal(ExitCodes.Success, await host.RunAsync("--verbose", "--version"));
        Assert.StartsWith("NetPrints.Cli ", host.Output.Trim(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ABareVerboseBehavesLikeABareInvocation()
    {
        var bare = new CliTestHost();
        var verbose = new CliTestHost();

        int bareExitCode = await bare.RunAsync();
        int verboseExitCode = await verbose.RunAsync("--verbose");

        Assert.Equal(bareExitCode, verboseExitCode);
        Assert.Equal(bare.Output, verbose.Output);
    }

    [Fact]
    public async Task VersionPrintsTheApplicationAndItsInformationalVersion()
    {
        var host = new CliTestHost();
        string? informational = typeof(CliApplication).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        Assert.Equal(ExitCodes.Success, await host.RunAsync("--version"));
        Assert.Equal("NetPrints.Cli " + informational, host.Output.Trim());
    }

    [Theory]
    [MemberData(nameof(CommandNames))]
    public async Task EveryCommandsHelpExitsZero(string command)
    {
        var host = new CliTestHost();

        Assert.Equal(ExitCodes.Success, await host.RunAsync(command, "--help"));
        Assert.Contains("USAGE", host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void ThereIsAtLeastTheBuildCommand()
    {
        Assert.Contains(CliCommandCatalog.All, command => command.Name == "build");
    }

    [Theory]
    [InlineData("frobnicate")]
    [InlineData("build", "--no-such-option")]
    [InlineData("build", "--verbose=maybe")]
    public async Task UnknownCommandOptionOrValueExitsWithUsage(params string[] args)
    {
        var host = new CliTestHost();

        Assert.Equal(ExitCodes.Usage, await host.RunAsync(args));
        Assert.NotEmpty(host.Error.ToString());
    }

    [Theory]
    [InlineData("-p", "x.csproj", "-r")]
    [InlineData("--project-path", "x.csproj")]
    [InlineData("--project-path=x.csproj")]
    [InlineData("--run")]
    [InlineData("--verbose", "-r")]
    public async Task ProjectOneFlagsAreRejectedWithTheReplacementMessage(params string[] args)
    {
        var host = new CliTestHost();

        Assert.Equal(ExitCodes.Usage, await host.RunAsync(args));
        Assert.Equal(
            "The -p/--project-path and -r/--run options were replaced: use 'netprints build <project>' or 'netprints run <project>'."
            + Environment.NewLine,
            host.Error.ToString());
    }

    [Fact]
    public async Task AProjectOneFlagAfterTheCommandNameIsAnOrdinaryUnknownOption()
    {
        var host = new CliTestHost();

        Assert.Equal(ExitCodes.Usage, await host.RunAsync("build", "-r"));
    }

    [Theory]
    [InlineData(false, "--verbose", "build")]
    [InlineData(true, "--verbose", "build")]
    [InlineData(true, "build", "--verbose")]
    public async Task ACommandThatThrowsExitsWithInternalErrorAndTheStackTraceOnlyWithVerbose(bool verbose, params string[] args)
    {
        var host = NewHostInTempDirectory();
        string project = Path.Combine(host.Environment.CurrentDirectory, "App.csproj");
        await File.WriteAllTextAsync(project, "<Project />", TestContext.Current.CancellationToken);
        host.Projects.ThrowOnBuild = new InvalidOperationException("boom");

        string[] arguments = verbose ? args : args.Where(arg => arg != "--verbose").ToArray();
        Assert.Equal(ExitCodes.InternalError, await host.RunAsync(arguments));

        string error = host.Error.ToString();
        Assert.Contains("boom", error, StringComparison.Ordinal);
        Assert.Equal(verbose, error.Contains("   at ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CancellationIsReportedAsExit130WithoutAnInternalError()
    {
        var host = NewHostInTempDirectory();
        string project = Path.Combine(host.Environment.CurrentDirectory, "App.csproj");
        await File.WriteAllTextAsync(project, "<Project />", TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        host.Projects.ThrowOnBuild = new OperationCanceledException(cancellation.Token);
        await cancellation.CancelAsync();

        Assert.Equal(ExitCodes.Canceled, await host.RunAsync(cancellation.Token, "build"));

        Assert.DoesNotContain("Internal error", host.Error.ToString(), StringComparison.Ordinal);
        Assert.Empty(host.Output);
    }

    [Fact]
    public async Task AnOperationCanceledExceptionWithoutARequestedCancellationIsStillAnInternalError()
    {
        var host = NewHostInTempDirectory();
        await File.WriteAllTextAsync(Path.Combine(host.Environment.CurrentDirectory, "App.csproj"), "<Project />", TestContext.Current.CancellationToken);
        host.Projects.ThrowOnBuild = new OperationCanceledException("timed out");

        Assert.Equal(ExitCodes.InternalError, await host.RunAsync("build"));
    }

    private sealed class MissingService;

    private sealed class NeedsMissingServiceCommand(MissingService service) : AsyncCommand
    {
        public MissingService Service { get; } = service;

        public override Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken) => Task.FromResult(0);
    }

    [Fact]
    public async Task ACommandWithAnUnresolvableDependencyIsAnInternalErrorNotAUsageError()
    {
        var host = new CliTestHost();
        CliCommand[] commands = [new("broken", config => config.AddCommand<NeedsMissingServiceCommand>("broken"))];

        int exitCode = await CliApplication.RunAsync(["broken"], host.Services, commands, TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.InternalError, exitCode);
        Assert.Contains("Internal error", host.Error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("build", "--verbose=maybe")]
    [InlineData("generate", "--graph", "")]
    public async Task ConversionAndValidationFailuresRemainUsageErrors(params string[] args)
    {
        var host = new CliTestHost();

        Assert.Equal(ExitCodes.Usage, await host.RunAsync(args));
    }

    [Fact]
    public async Task OutputRedirectedToAFileCarriesNoAnsiEscape()
    {
        using var writer = new StringWriter();
        var environment = new CliTestHost().Environment;
        var host = new CliTestHost { Console = CliServices.CreateConsole(writer, isRedirected: true, environment) };

        Assert.Equal(ExitCodes.Success, await host.RunAsync("--help"));

        Assert.Contains("USAGE", writer.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Escape, writer.ToString());
    }

    [Fact]
    public async Task NoColorSuppressesAnsiEvenOnATerminal()
    {
        using var writer = new StringWriter();
        var variables = new System.Collections.Generic.Dictionary<string, string?> { ["NO_COLOR"] = "1" };
        var environment = new CliTestHost(variables: variables).Environment;
        var noColorHost = new CliTestHost(variables: variables)
        {
            Console = CliServices.CreateConsole(writer, isRedirected: false, environment),
        };

        Assert.Equal(ExitCodes.Success, await noColorHost.RunAsync("--help"));

        Assert.DoesNotContain(Escape, writer.ToString());
    }

    [Theory]
    [InlineData(true, false, AnsiSupport.No)]
    [InlineData(false, true, AnsiSupport.No)]
    [InlineData(false, false, AnsiSupport.Detect)]
    public void ConsoleSettingsFollowRedirectionAndNoColor(bool redirected, bool noColor, AnsiSupport expected)
    {
        var variables = noColor ? new System.Collections.Generic.Dictionary<string, string?> { ["NO_COLOR"] = "1" } : [];
        var environment = new CliTestHost(variables: variables).Environment;

        AnsiConsoleSettings settings = CliServices.ConsoleSettings(redirected, environment);

        Assert.Equal(expected, settings.Ansi);
        if (expected == AnsiSupport.No)
        {
            Assert.Equal(ColorSystemSupport.NoColors, settings.ColorSystem);
        }
    }

    [Fact]
    public async Task EveryRegisteredCommandExampleValidates()
    {
        var host = new CliTestHost();
        var app = new CommandApp(new TypeRegistrar(host.Services));
        app.Configure(config =>
        {
            CliApplication.ConfigureCommands(config);
            config.ConfigureConsole(host.Console);
            config.PropagateExceptions();
            config.ValidateExamples();
        });

        Assert.Equal(0, await app.RunAsync(["--help"], TestContext.Current.CancellationToken));
    }
}
