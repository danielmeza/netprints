using Microsoft.CodeAnalysis.Text;

namespace NetPrints.RepoAnalyzers.Style;

/// <summary>Line break and indentation helpers that keep a file's own line ending.</summary>
internal static class LineBreaks
{
    private const string CarriageReturnLineFeed = "\r\n";

    private const string LineFeed = "\n";

    private const int CarriageReturnLineFeedLength = 2;

    /// <summary>The line break the file uses, read from its first line.</summary>
    /// <param name="text">The file text.</param>
    /// <returns><c>\r\n</c> when the first line ends with it, otherwise <c>\n</c>.</returns>
    public static string For(SourceText text)
    {
        TextLine first = text.Lines[0];
        return first.EndIncludingLineBreak - first.End == CarriageReturnLineFeedLength ? CarriageReturnLineFeed : LineFeed;
    }

    /// <summary>The leading whitespace of the line that holds a position.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="position">A position in the line.</param>
    /// <returns>The line's indentation.</returns>
    public static string IndentOf(SourceText text, int position)
    {
        TextLine line = text.Lines.GetLineFromPosition(position);
        int end = line.Start;
        while (end < line.End && (text[end] == ' ' || text[end] == '\t'))
        {
            end++;
        }

        return text.ToString(TextSpan.FromBounds(line.Start, end));
    }
}
