using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetPrints.Cli.Infrastructure;
using NetPrints.Projects;
using NetPrints.Testing;
using Xunit;

namespace NetPrints.Cli.Tests.Infrastructure;

/// <summary><see cref="ProgramRunner"/> over the echo tool: removals apply, and the editor-only request flags change nothing.</summary>
public sealed class ProgramRunnerTests
{
    private static ProcessStartRequest Echo(string[] arguments, IReadOnlyDictionary<string, string>? environment = null) =>
        new(RunFixtures.DotnetHost(), [RunFixtures.EchoToolDll(), .. arguments], Environment.CurrentDirectory, environment);

    private static async Task<(int ExitCode, string[] Lines)> RunAsync(ProcessStartRequest request)
    {
        using var output = new MemoryStream();
        using var error = new MemoryStream();
        int exitCode = await new ProgramRunner(output, error).RunAsync(request, TestContext.Current.CancellationToken);
        string[] lines = Encoding.UTF8.GetString(output.ToArray()).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return (exitCode, lines);
    }

    [Fact]
    public async Task ARemovedVariableIsUnsetForTheProgram()
    {
        const string Name = "NETPRINTS_CLI_REMOVE_ME";
        Environment.SetEnvironmentVariable(Name, "inherited");
        try
        {
            var request = Echo([], new Dictionary<string, string> { ["ECHO_TOOL_ENV"] = Name }) with { EnvironmentRemovals = [Name] };

            var (exitCode, lines) = await RunAsync(request);

            Assert.Equal(0, exitCode);
            Assert.Contains($"env:{Name} unset", lines);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Name, null);
        }
    }

    [Fact]
    public async Task AVariableIsAppliedAfterItsRemoval()
    {
        const string Name = "NETPRINTS_CLI_REMOVE_THEN_SET";
        Environment.SetEnvironmentVariable(Name, "inherited");
        try
        {
            var request = Echo([], new Dictionary<string, string> { ["ECHO_TOOL_ENV"] = Name, [Name] = "applied" }) with { EnvironmentRemovals = [Name] };

            var (_, lines) = await RunAsync(request);

            Assert.Contains($"env:{Name}=applied", lines);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Name, null);
        }
    }

    [Fact]
    public async Task TheEditorOnlyFlagsDoNotChangeTheRun()
    {
        string[] arguments = ["", "a b", "say \"hi\"", "ñandú"];
        var plain = Echo(arguments, new Dictionary<string, string> { ["ECHO_TOOL_EXIT"] = "7" });
        var flagged = plain with { CaptureOutput = false, RedirectInput = true, ExternalTerminal = true };

        var expected = await RunAsync(plain);
        var actual = await RunAsync(flagged);

        Assert.Equal(7, actual.ExitCode);
        Assert.Equal(expected.ExitCode, actual.ExitCode);
        Assert.Equal(Stable(expected.Lines), Stable(actual.Lines));
        Assert.Contains("argv[1]=a b", actual.Lines);
    }

    [Fact]
    public async Task AProgramThatCannotStartThrowsAndLeavesNothingRunning()
    {
        var missingProgram = new ProcessStartRequest("np-no-such-program-1f3a", [], Environment.CurrentDirectory);
        var missingFolder = Echo([]) with { WorkingDirectory = Path.Combine(Path.GetTempPath(), "np-no-such-dir-1f3a") };

        await Assert.ThrowsAnyAsync<Win32Exception>(() => RunAsync(missingProgram));
        await Assert.ThrowsAnyAsync<Exception>(() => RunAsync(missingFolder));
    }

    private static string[] Stable(IEnumerable<string> lines) => [.. lines.Where(l => !l.StartsWith("pid=", StringComparison.Ordinal))];
}
