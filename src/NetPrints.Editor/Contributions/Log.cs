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
}
