#nullable enable
using System.Collections.Generic;

namespace NetPrints.Projects;

/// <summary>
/// Describes a process to start: <see cref="IProjectSystem.GetRunCommand"/>'s <c>dotnet run</c>
/// invocation, or the request an <see cref="IProcessRunner"/> executes (project-system.md §4).
/// </summary>
/// <param name="FileName">Executable to start (e.g. <c>"dotnet"</c>).</param>
/// <param name="Arguments">Command-line arguments, one per element (never a single, shell-quoted
/// string).</param>
/// <param name="WorkingDirectory">Working directory the process is started in.</param>
/// <param name="EnvironmentVariables">Environment variables to set (or override) on top of the
/// current process's own environment, or <see langword="null"/> to inherit it unchanged
/// (<see cref="IProjectSystem.BuildAsync"/> uses this for <c>DOTNET_CLI_UI_LANGUAGE</c> and
/// <c>MSBUILDTERMINALLOGGER</c>, project-system.md §4).</param>
public sealed record ProcessStartRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string>? EnvironmentVariables = null);
