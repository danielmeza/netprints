using Microsoft.Extensions.Logging;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Process-wide services a host (desktop, headless tests) creates once and passes to
/// <see cref="EditorComposition"/>, as opposed to the per-editor-scope services
/// <see cref="EditorContext"/> bundles (editor-services.md §4). More members are added as later
/// tasks wire them up (extensions, settings, the host channel).
/// </summary>
/// <param name="LoggerFactory">Creates the loggers every editor-scope service logs through.</param>
/// <param name="MsBuildAvailable">
/// Result of <c>MsBuildRegistration.EnsureRegistered()</c>, run once by the host before any
/// <c>Microsoft.Build</c>-namespace type is loaded. When <see langword="false"/>,
/// <see cref="EditorComposition"/> wires up a <see cref="NoSdkProjectSystem"/> instead of
/// <c>MsBuildProjectSystem</c> (project-system.md §4, PS-T13).
/// </param>
public sealed record EditorHostServices(ILoggerFactory LoggerFactory, bool MsBuildAvailable);
