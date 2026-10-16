using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Editor;

/// <summary>A segment of the status bar.</summary>
/// <param name="Id">The namespaced id.</param>
/// <param name="Alignment">The side of the bar the segment sits on.</param>
/// <param name="Order">The position within its side, lowest first.</param>
/// <param name="CreateViewModel">Creates the segment's view model; its view is found by data template.</param>
/// <param name="AppliesTo">Tells whether the segment is shown for the current state of the editor; <see langword="null"/> shows it always. A segment it rejects is hidden.</param>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed record StatusBarItemDescriptor(string Id, StatusBarAlignment Alignment, int Order, Func<IStatusBarContext, object> CreateViewModel, Func<IStatusBarContext, bool>? AppliesTo = null);
