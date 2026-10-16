using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Editor;

/// <summary>A dialog an extension shows by id through <see cref="IDialogService.ShowDialogAsync"/>.</summary>
/// <param name="Id">The namespaced id.</param>
/// <param name="Title">The non-empty window title.</param>
/// <param name="CreateViewModel">Creates the dialog's view model each time the dialog is shown; its view is found by data template.</param>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed record DialogDescriptor(string Id, string Title, Func<IDialogContext, object> CreateViewModel);
