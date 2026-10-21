using Microsoft.Extensions.Logging;

namespace NetPrints.Editor.Contributions;

/// <summary>Source-generated log messages for <c>NetPrints.Editor.Contributions</c>.</summary>
internal static partial class Log
{
    /// <summary>Logs 1070: the registry rejected or altered a contribution.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="kind">The kind of issue.</param>
    /// <param name="id">The id of the contribution.</param>
    /// <param name="owner">The contributor.</param>
    /// <param name="issueMessage">The description.</param>
    [LoggerMessage(EventId = 1070, Level = LogLevel.Warning, Message = "Contribution issue {Kind} for {Id} (owner {Owner}): {IssueMessage}")]
    public static partial void ContributionIssue(ILogger logger, ContributionIssueKind kind, string id, string owner, string issueMessage);

    /// <summary>Logs 1071 (NPX011): a provider, view model factory or predicate of an extension threw; it is skipped, and this is logged once per contribution and session.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">What the extension threw.</param>
    /// <param name="extension">The display name of the extension.</param>
    /// <param name="contribution">What failed.</param>
    [LoggerMessage(EventId = 1071, Level = LogLevel.Warning, Message = "NPX011: {Contribution} of extension {Extension} failed and is skipped")]
    public static partial void ContributionFailed(ILogger logger, Exception exception, string extension, string contribution);

    /// <summary>Logs 1072: the handler of a command of an extension threw while running.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="exception">What the extension threw.</param>
    /// <param name="extension">The display name of the extension.</param>
    /// <param name="commandId">The command.</param>
    [LoggerMessage(EventId = 1072, Level = LogLevel.Error, Message = "The command {CommandId} of extension {Extension} failed")]
    public static partial void HandlerFailed(ILogger logger, Exception exception, string extension, string commandId);
}
