using Microsoft.Extensions.Logging;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Source-generated log messages for <c>NetPrints.Editor.Hosting</c> (editor-services.md §6).
/// </summary>
internal static partial class Log
{
    /// <summary>Logs 1001: an exception reached the UI thread unhandled.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">The exception that escaped to the UI thread.</param>
    [LoggerMessage(EventId = 1001, Level = LogLevel.Error, Message = "Unhandled exception on the UI thread")]
    public static partial void UnhandledUiException(ILogger logger, Exception exception);

    /// <summary>Logs 1002: a task's exception was never observed.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">The unobserved exception.</param>
    [LoggerMessage(EventId = 1002, Level = LogLevel.Error, Message = "Unobserved task exception")]
    public static partial void UnobservedTaskException(ILogger logger, Exception exception);

    /// <summary>Logs 1010: <see cref="ReflectionHost.ReloadAsync"/> started rebuilding the provider for a project.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="projectName">Name of the project being reloaded.</param>
    [LoggerMessage(EventId = 1010, Level = LogLevel.Debug, Message = "Reloading types for {ProjectName}")]
    public static partial void ReflectionReloadStarted(ILogger logger, string projectName);

    /// <summary>Logs 1011: <see cref="ReflectionHost.ReloadAsync"/> finished rebuilding and published the new provider.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="projectName">Name of the reloaded project.</param>
    /// <param name="elapsedMs">Time the reload took, in milliseconds.</param>
    /// <param name="assemblyCount">Number of reference assemblies the provider was built from.</param>
    [LoggerMessage(EventId = 1011, Level = LogLevel.Information, Message = "Types loaded for {ProjectName} in {ElapsedMs} ms ({AssemblyCount} assemblies)")]
    public static partial void ReflectionReloadCompleted(ILogger logger, string projectName, long elapsedMs, int assemblyCount);

    /// <summary>Logs 1012: one message from the project system snapshot a reload was built from.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="code">The message's stable code.</param>
    /// <param name="message">The message's human-readable text.</param>
    [LoggerMessage(EventId = 1012, Level = LogLevel.Warning, Message = "{Code}: {Message}")]
    public static partial void ProjectMessage(ILogger logger, string code, string message);

    /// <summary>Logs 1013: <see cref="ReflectionHost.ReloadAsync"/> failed to rebuild the provider.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="projectName">Name of the project the reload was for.</param>
    /// <param name="exception">The exception that made the reload fail.</param>
    [LoggerMessage(EventId = 1013, Level = LogLevel.Error, Message = "Loading types for {ProjectName} failed")]
    public static partial void ReflectionReloadFailed(ILogger logger, string projectName, Exception exception);

    /// <summary>Logs 1020: a message arrived on the host channel.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="type">The message type.</param>
    [LoggerMessage(EventId = 1020, Level = LogLevel.Debug, Message = "Host message {Type}")]
    public static partial void HostMessageReceived(ILogger logger, string type);

    /// <summary>Logs 1021: <c>NETPRINTS_HOST_CHANNEL</c> names a factory no loaded extension provides.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="id">The requested factory id.</param>
    [LoggerMessage(EventId = 1021, Level = LogLevel.Error, Message = "Host channel {Id} is not provided by any extension")]
    public static partial void HostChannelUnknown(ILogger logger, string id);

    /// <summary>Logs 1022: a host channel message the editor does not understand, or cannot act on, was ignored.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="type">The message type.</param>
    [LoggerMessage(EventId = 1022, Level = LogLevel.Debug, Message = "Ignoring host message {Type}")]
    public static partial void HostMessageIgnored(ILogger logger, string type);

    /// <summary>Logs 1023: the factory of the requested host channel threw.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">The exception the factory threw.</param>
    /// <param name="id">The requested factory id.</param>
    [LoggerMessage(EventId = 1023, Level = LogLevel.Error, Message = "Host channel {Id} could not be created")]
    public static partial void HostChannelCreateFailed(ILogger logger, Exception exception, string id);

    /// <summary>Logs 1024: disposing services on shutdown threw; the process shuts down anyway.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">The exception disposal threw.</param>
    [LoggerMessage(EventId = 1024, Level = LogLevel.Error, Message = "Shutdown cleanup failed; exiting anyway")]
    public static partial void ShutdownCleanupFailed(ILogger logger, Exception exception);

    /// <summary>Logs 1025: host services finished disposing during shutdown, right before the process
    /// exits (R2-05: replaces a bespoke stderr marker the E2E <c>ShutdownTests</c> greps for instead).</summary>
    /// <param name="logger">Logger to write to.</param>
    [LoggerMessage(EventId = 1025, Level = LogLevel.Information, Message = "Host services disposed")]
    public static partial void HostServicesDisposed(ILogger logger);

    /// <summary>Logs 1030: a fire-and-forget task passed to <see cref="TaskExtensions.Forget(Task, ILogger)"/> faulted.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">The task's (unwrapped) exception.</param>
    [LoggerMessage(EventId = 1030, Level = LogLevel.Error, Message = "Fire-and-forget task faulted")]
    public static partial void TaskFaulted(ILogger logger, Exception exception);
}
