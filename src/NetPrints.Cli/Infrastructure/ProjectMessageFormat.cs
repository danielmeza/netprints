using System;
using System.Collections.Generic;
using System.IO;
using NetPrints.Projects;

namespace NetPrints.Cli.Infrastructure;

/// <summary>Formats the messages of the project system as the canonical <c>file(line,column): code: message</c> lines.</summary>
internal static class ProjectMessageFormat
{
    /// <summary>Formats one message.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The line, without a trailing newline.</returns>
    public static string ToLine(ProjectMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        string location = message.File is null ? "" : message.Line is null ? $"{message.File}: " : $"{message.File}({message.Line},{message.Column}): ";
        return $"{location}{message.Code}: {message.Message}";
    }

    /// <summary>Writes every <see cref="ProjectMessageSeverity.Error"/> message.</summary>
    /// <param name="messages">The messages of a loaded project.</param>
    /// <param name="writer">Receives one line per error.</param>
    /// <returns><see langword="true"/> when at least one error was written.</returns>
    public static bool WriteErrors(IEnumerable<ProjectMessage> messages, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(writer);
        bool any = false;
        foreach (ProjectMessage message in messages)
        {
            if (message.Severity == ProjectMessageSeverity.Error)
            {
                writer.WriteLine(ToLine(message));
                any = true;
            }
        }

        return any;
    }
}
