using Microsoft.Extensions.Logging;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Process-wide services a host (desktop, headless tests) creates once and passes to
/// <see cref="EditorComposition"/>, as opposed to the per-editor-scope services
/// <see cref="EditorContext"/> bundles (editor-services.md §4). More members are added as later
/// tasks wire them up (extensions, settings, the host channel, MSBuild availability).
/// </summary>
/// <param name="LoggerFactory">Creates the loggers every editor-scope service logs through.</param>
public sealed record EditorHostServices(ILoggerFactory LoggerFactory);
