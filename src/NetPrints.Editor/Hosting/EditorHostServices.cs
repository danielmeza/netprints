using Microsoft.Extensions.Logging;
using NetPrints.Extensibility.Hosting;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Process-wide services a host (desktop, headless tests) creates once and passes to
/// <see cref="EditorComposition"/>, as opposed to the per-editor-scope services
/// <see cref="EditorContext"/> bundles (editor-services.md §4).
/// </summary>
/// <param name="LoggerFactory">Creates the loggers every editor-scope service logs through.</param>
/// <param name="Extensions">Holds the registry of loaded extensions; the editor asks it to load a trusted
/// project's extension folders when the project opens. The host owns and disposes it.</param>
/// <param name="Settings">Reads and writes the user's settings file.</param>
/// <param name="HostChannel">The one channel to the application hosting the editor; the host owns and
/// disposes it.</param>
/// <param name="HostChannelError">Why <paramref name="HostChannel"/> is not the channel the environment asked
/// for (<see cref="HostChannelSelector"/>), or <see langword="null"/>; shown once at startup.</param>
/// <param name="MsBuildAvailable">
/// Result of <c>MsBuildRegistration.EnsureRegistered()</c>, run once by the host before any
/// <c>Microsoft.Build</c>-namespace type is loaded. When <see langword="false"/>,
/// <see cref="EditorComposition"/> wires up a <see cref="NoSdkProjectSystem"/> instead of
/// <c>MsBuildProjectSystem</c> (project-system.md §4, PS-T13).
/// </param>
public sealed record EditorHostServices(
    ILoggerFactory LoggerFactory,
    IExtensionHost Extensions,
    ISettingsStore Settings,
    IHostChannel HostChannel,
    string? HostChannelError,
    bool MsBuildAvailable);
