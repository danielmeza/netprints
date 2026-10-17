namespace NetPrints.Editor.Contributions;

/// <summary>An extension's command handler failed; the message names the extension and the command (FR-034).</summary>
public sealed class ExtensionFailureException : Exception
{
    /// <summary>Creates the exception without details.</summary>
    public ExtensionFailureException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The message.</param>
    public ExtensionFailureException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The exception the extension threw.</param>
    public ExtensionFailureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception for a command of an extension.</summary>
    /// <param name="extensionName">The display name of the extension.</param>
    /// <param name="commandId">The id of the command.</param>
    /// <param name="innerException">The exception the extension threw.</param>
    public ExtensionFailureException(string extensionName, string commandId, Exception innerException)
        : base(TextFormat.Of(Strings.ContributionError_HandlerFailed, extensionName, commandId, innerException?.Message), innerException)
    {
    }
}
