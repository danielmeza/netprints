using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Editor;

/// <summary>A dialog an extension shows by id through <see cref="IDialogService.ShowDialogAsync"/>.</summary>
/// <param name="Id">The namespaced id.</param>
/// <param name="Title">The non-empty window title. A plain string is a literal that takes the convention key <c>Dialog.&lt;id&gt;.Title</c> and shows itself when no resource has the key.</param>
/// <param name="CreateViewModel">Creates the dialog's view model each time the dialog is shown; its view is found by data template.</param>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed record DialogDescriptor(string Id, LocalizedText Title, Func<IDialogContext, object> CreateViewModel)
{
    /// <summary>Gets the title with its convention key (FR-027): a literal title is keyed <c>Dialog.&lt;id&gt;.Title</c>.</summary>
    public LocalizedText Title { get; init; } = ConventionKeys.Dialog(Id, Title);
}
