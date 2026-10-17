using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Extensibility.Editor;

namespace NetPrints.Editor.Shell;

/// <summary>A status bar segment view model that follows editor state: the bar asks it to refresh whenever the state may have changed.</summary>
public interface IStatusSegment
{
    /// <summary>Reads the state again.</summary>
    void Refresh();
}

/// <summary>One segment of the status bar: the descriptor, and the view model its factory created.</summary>
/// <param name="descriptor">The descriptor.</param>
/// <param name="content">The view model the descriptor's factory created; its view is found by data template.</param>
public sealed class StatusBarItemViewModel(StatusBarItemDescriptor descriptor, object content) : ObservableObject
{
    /// <summary>Gets the descriptor's id.</summary>
    public string Id => descriptor.Id;

    /// <summary>Gets the side of the bar the segment sits on.</summary>
    public StatusBarAlignment Alignment => descriptor.Alignment;

    /// <summary>Gets the position within its side, lowest first.</summary>
    public int Order => descriptor.Order;

    /// <summary>Gets the segment's view model.</summary>
    public object Content { get; } = content;

    /// <summary>Gets the descriptor, to ask whether it applies.</summary>
    internal StatusBarItemDescriptor Descriptor => descriptor;
}
