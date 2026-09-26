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
public sealed record ProcessStartRequest(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory);
