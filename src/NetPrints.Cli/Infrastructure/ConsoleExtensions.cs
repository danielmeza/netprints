using System;
using Spectre.Console;

namespace NetPrints.Cli.Infrastructure;

/// <summary>Plain-text output helpers: no markup parsing and no wrapping, so paths and diagnostics stay verbatim.</summary>
internal static class ConsoleExtensions
{
    /// <summary>Writes one line of verbatim text to the console's output.</summary>
    /// <param name="console">The console.</param>
    /// <param name="text">The text; markup is not interpreted.</param>
    public static void WriteLineRaw(this IAnsiConsole console, string text)
    {
        ArgumentNullException.ThrowIfNull(console);
        console.Profile.Out.Writer.WriteLine(text);
    }
}
