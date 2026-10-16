using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Editor;

/// <summary>Supplies results to go-to-anything.</summary>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public interface IGoToProvider
{
    /// <summary>Gets the group the results appear under: Graphs, Nodes, Variables, Methods or Commands.</summary>
    string Kind { get; }

    /// <summary>Searches for items matching <paramref name="text"/>.</summary>
    /// <param name="text">The text typed by the user.</param>
    /// <param name="cancellationToken">Cancels the search when the text changes.</param>
    /// <returns>The matches, produced lazily.</returns>
    IAsyncEnumerable<GoToItem> SearchAsync(string text, CancellationToken cancellationToken);
}
