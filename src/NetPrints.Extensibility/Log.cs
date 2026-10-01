using Microsoft.Extensions.Logging;

namespace NetPrints.Extensibility;

/// <summary>
/// Source-generated log messages for <c>NetPrints.Extensibility</c> (editor-services.md §6).
/// </summary>
internal static partial class Log
{
    /// <summary>Logs 2001: the loader found an extension manifest.</summary>
    [LoggerMessage(EventId = 2001, Level = LogLevel.Debug, Message = "Found extension {Id} at {ManifestPath}")]
    public static partial void ExtensionDiscovered(ILogger logger, string id, string manifestPath);

    /// <summary>Logs 2002: an extension loaded.</summary>
    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Loaded extension {Id} {Version}")]
    public static partial void ExtensionLoaded(ILogger logger, string id, string version);

    /// <summary>Logs 2003: an extension failed to load.</summary>
    [LoggerMessage(EventId = 2003, Level = LogLevel.Error, Message = "Extension {Id} failed: {Code} {Reason}")]
    public static partial void ExtensionLoadFailed(ILogger logger, Exception? exception, string id, string code, string reason);

    /// <summary>Logs 2004: a second extension with an id that is already loaded was ignored.</summary>
    [LoggerMessage(EventId = 2004, Level = LogLevel.Warning, Message = "Extension {Id} at {ManifestPath} ignored: already loaded")]
    public static partial void ExtensionDuplicate(ILogger logger, string id, string manifestPath);

    /// <summary>Logs 2005: a node kind was rejected.</summary>
    [LoggerMessage(EventId = 2005, Level = LogLevel.Error, Message = "Node kind {Kind} from {Id} rejected: {Reason}")]
    public static partial void NodeKindConflict(ILogger logger, string kind, string id, string reason);

    /// <summary>Logs 2006: a settings section could not be read and its default is used; the section stays on disk.</summary>
    [LoggerMessage(EventId = 2006, Level = LogLevel.Warning, Message = "Settings section {Section} in {FilePath} is invalid; using its default")]
    public static partial void SettingsSectionInvalid(ILogger logger, Exception? exception, string section, string filePath);

    /// <summary>Logs 2007: a contribution other than a node kind was rejected.</summary>
    [LoggerMessage(EventId = 2007, Level = LogLevel.Error, Message = "Contribution {Contribution} from {Id} rejected: {Reason}")]
    public static partial void ContributionRejected(ILogger logger, string contribution, string id, string reason);

    /// <summary>Logs 2008: a search directory does not exist and was skipped.</summary>
    [LoggerMessage(EventId = 2008, Level = LogLevel.Debug, Message = "Extension search directory {Directory} does not exist")]
    public static partial void SearchDirectoryMissing(ILogger logger, string directory);

    /// <summary>Logs 2009: disposing a contribution failed.</summary>
    [LoggerMessage(EventId = 2009, Level = LogLevel.Warning, Message = "Disposing {Contribution} failed")]
    public static partial void DisposeFailed(ILogger logger, Exception exception, string contribution);

    /// <summary>Logs 2010: an extension folder holds a copy of an assembly the host provides; the host's copy is used.</summary>
    [LoggerMessage(EventId = 2010, Level = LogLevel.Warning, Message = "Extension {Id} ships {AssemblyName} at {Path}; the host provides it, so the copy is ignored")]
    public static partial void HostAssemblyShadowed(ILogger logger, string id, string assemblyName, string path);

    /// <summary>Logs 2011: an extension folder holds a copy of an assembly a dependency provides; the dependency's copy is used.</summary>
    [LoggerMessage(EventId = 2011, Level = LogLevel.Warning, Message = "Extension {Id} ships {AssemblyName}; dependency {DependencyId} provides it, so the copy is ignored")]
    public static partial void DependencyAssemblyShadowed(ILogger logger, string id, string assemblyName, string dependencyId);

    /// <summary>Logs 2012: checking an extension folder for shadowed assemblies failed; the extension still loads.</summary>
    [LoggerMessage(EventId = 2012, Level = LogLevel.Debug, Message = "Shadow check of extension {Id} folder {Folder} failed")]
    public static partial void ShadowCheckFailed(ILogger logger, Exception exception, string id, string folder);
}
