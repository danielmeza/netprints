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
}
