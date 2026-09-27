using NetPrints.Core;

namespace NetPrints.Editor.Search;

/// <summary>
/// Search suggestion (US4) offered on empty canvas of an <see cref="EventGraph"/>: creates a custom
/// event entry (<see cref="NetPrints.Graph.EventEntryNode(EventGraph, string)"/>) named uniquely
/// against the class's methods and every event graph's entries.
/// </summary>
public sealed record CustomEventSuggestion
{
    /// <summary>Row text for this suggestion (<see cref="SuggestionItem.Describe"/>).</summary>
    public const string DisplayText = "Custom Event";

    /// <summary>
    /// Base name for the created entry, before <c>NetPrintsUtil.GetUniqueName</c> uniquifies it
    /// (<see cref="SuggestionListVM.SelectAsync"/>).
    /// </summary>
    public const string NamePrefix = "CustomEvent";
}

/// <summary>
/// Search suggestion (US4) offered on empty canvas of an <see cref="EventGraph"/>: creates an override
/// entry (<see cref="NetPrints.Graph.EventEntryNode(EventGraph, MethodSpecifier)"/>) for a base method
/// not already used by the class's methods or event graph entries.
/// </summary>
/// <param name="Method">The base method this entry would override.</param>
public sealed record OverrideEventSuggestion(MethodSpecifier Method);
