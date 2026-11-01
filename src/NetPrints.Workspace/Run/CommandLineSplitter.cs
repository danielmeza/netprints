using System;
using System.Collections.Generic;
using System.Text;

namespace NetPrints.Workspace.Run;

/// <summary>
/// Splits an argument string into a list and joins a list into a string, with the rules of <c>CommandLineToArgvW</c> on every OS
/// (the same rules <c>Process.ParseArgumentsIntoList</c> applies on Unix).
/// </summary>
public static class CommandLineSplitter
{
    private const char Quote = '"';
    private const char Backslash = '\\';
    private const int Pair = 2;

    /// <summary>
    /// Splits a command line into arguments. Space and tab outside quotes end an argument, and runs of them collapse.
    /// A run of n backslashes before a quote gives n/2 backslashes, and an odd n makes the quote a literal; a run not
    /// followed by a quote stays as written. Inside quotes, <c>""</c> is one literal quote. A quoted empty string is an empty argument.
    /// </summary>
    /// <param name="commandLine">The text to split.</param>
    /// <returns>The arguments, in order.</returns>
    public static IReadOnlyList<string> Split(string commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);

        var arguments = new List<string>();
        var current = new StringBuilder();
        int position = 0;
        while (position < commandLine.Length)
        {
            if (IsSeparator(commandLine[position]))
            {
                position++;
                continue;
            }

            current.Clear();
            position = ReadArgument(commandLine, position, current);
            arguments.Add(current.ToString());
        }

        return arguments;
    }

    /// <summary>Joins arguments into one string that <see cref="Split"/> reads back unchanged.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The text, with one space between arguments; an argument is quoted only when it is empty or holds a space, a tab or a quote.</returns>
    public static string Join(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var text = new StringBuilder();
        for (int index = 0; index < arguments.Count; index++)
        {
            if (index > 0)
            {
                text.Append(' ');
            }

            AppendArgument(text, arguments[index]);
        }

        return text.ToString();
    }

    private static bool IsSeparator(char character) => character is ' ' or '\t';

    private static int ReadArgument(string commandLine, int start, StringBuilder current)
    {
        bool inQuotes = false;
        int position = start;
        while (position < commandLine.Length)
        {
            char character = commandLine[position];
            if (character == Backslash)
            {
                position = ReadBackslashes(commandLine, position, current);
            }
            else if (character == Quote)
            {
                if (inQuotes && position + 1 < commandLine.Length && commandLine[position + 1] == Quote)
                {
                    current.Append(Quote);
                    position += Pair;
                }
                else
                {
                    inQuotes = !inQuotes;
                    position++;
                }
            }
            else if (IsSeparator(character) && !inQuotes)
            {
                break;
            }
            else
            {
                current.Append(character);
                position++;
            }
        }

        return position;
    }

    private static int ReadBackslashes(string commandLine, int start, StringBuilder current)
    {
        int position = start;
        while (position < commandLine.Length && commandLine[position] == Backslash)
        {
            position++;
        }

        int count = position - start;
        if (position < commandLine.Length && commandLine[position] == Quote)
        {
            current.Append(Backslash, count / Pair);
            if (count % Pair != 0)
            {
                current.Append(Quote);
                position++;
            }
        }
        else
        {
            current.Append(Backslash, count);
        }

        return position;
    }

    private static void AppendArgument(StringBuilder text, string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);

        if (argument.Length > 0 && argument.AsSpan().IndexOfAny(' ', '\t', Quote) < 0)
        {
            text.Append(argument);
            return;
        }

        text.Append(Quote);
        int backslashes = 0;
        foreach (char character in argument)
        {
            if (character == Backslash)
            {
                backslashes++;
                continue;
            }

            if (character == Quote)
            {
                text.Append(Backslash, (backslashes * Pair) + 1);
            }
            else
            {
                text.Append(Backslash, backslashes);
            }

            backslashes = 0;
            text.Append(character);
        }

        text.Append(Backslash, backslashes * Pair);
        text.Append(Quote);
    }
}
