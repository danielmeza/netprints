namespace NetPrints.Editor.Hosting.Automation;

/// <summary>
/// Names of the read-only properties the automation tree reports for an element
/// (<see cref="AutomationElement.Properties"/>): the contract between <see cref="AutomationTree"/>
/// and the UI test page objects, so a rename breaks the build instead of a test run.
/// </summary>
public static class AutomationPropertyNames
{
    /// <summary>
    /// Reported by the graph grid background: its background color, as text.
    /// </summary>
    public const string BackgroundColor = nameof(BackgroundColor);
    /// <summary>
    /// Reported by every control: its cursor's name (see <see cref="EditorCursors.NameOf"/>), or <see langword="null"/> for the default cursor.
    /// </summary>
    public const string Cursor = nameof(Cursor);
    /// <summary>
    /// Reported by the graph canvas: the grid cell size in DIP, as text.
    /// </summary>
    public const string GridCellSize = nameof(GridCellSize);
    /// <summary>
    /// Reported by the graph grid background: which rendering mode it last used, as text.
    /// </summary>
    public const string GridRenderMode = nameof(GridRenderMode);
    /// <summary>
    /// Reported by the graph grid background: which rendering path it last used, as text.
    /// </summary>
    public const string GridRenderPath = nameof(GridRenderPath);
    /// <summary>
    /// Reported by an image: whether it has a source ("True"/"False").
    /// </summary>
    public const string HasSource = nameof(HasSource);
    /// <summary>
    /// Reported by every control: its bounds height in DIP, as text.
    /// </summary>
    public const string Height = nameof(Height);
    /// <summary>
    /// Reported by a toggle button: its checked state ("True", "False", or empty for indeterminate).
    /// </summary>
    public const string IsChecked = nameof(IsChecked);
    /// <summary>
    /// Reported by every control: whether it is effectively enabled ("True"/"False").
    /// </summary>
    public const string IsEnabled = nameof(IsEnabled);
    /// <summary>
    /// Reported by every control: whether it has input focus ("True"/"False").
    /// </summary>
    public const string IsFocused = nameof(IsFocused);
    /// <summary>
    /// Reported by every control: whether it or a descendant has keyboard focus ("True"/"False").
    /// </summary>
    public const string IsKeyboardFocusWithin = nameof(IsKeyboardFocusWithin);
    /// <summary>
    /// Reported by a popup: whether it is open ("True"/"False").
    /// </summary>
    public const string IsOpen = nameof(IsOpen);
    /// <summary>
    /// Reported by a text box: whether it is read-only ("True"/"False").
    /// </summary>
    public const string IsReadOnly = nameof(IsReadOnly);
    /// <summary>
    /// Reported by a graph item container (node or pin): whether it is selected ("True"/"False").
    /// </summary>
    public const string IsSelected = nameof(IsSelected);
    /// <summary>
    /// Reported by every control: whether it is effectively visible ("True"/"False").
    /// </summary>
    public const string IsVisible = nameof(IsVisible);
    /// <summary>
    /// Reported by a selecting items control (eg. a list): its item count, as text.
    /// </summary>
    public const string ItemCount = nameof(ItemCount);
    /// <summary>
    /// Reported by a graph item container: its canvas X position in DIP, as text.
    /// </summary>
    public const string LocationX = nameof(LocationX);
    /// <summary>
    /// Reported by a graph item container: its canvas Y position in DIP, as text.
    /// </summary>
    public const string LocationY = nameof(LocationY);
    /// <summary>
    /// Reported by the graph grid background: its major grid line color, as text.
    /// </summary>
    public const string MajorColor = nameof(MajorColor);
    /// <summary>
    /// Reported by the graph canvas: its maximum allowed zoom, as text.
    /// </summary>
    public const string MaxViewportZoom = nameof(MaxViewportZoom);
    /// <summary>
    /// Reported by the graph grid background: its minor grid line color, as text.
    /// </summary>
    public const string MinorColor = nameof(MinorColor);
    /// <summary>
    /// Reported by the graph canvas: its minimum allowed zoom, as text.
    /// </summary>
    public const string MinViewportZoom = nameof(MinViewportZoom);
    /// <summary>
    /// Reported by every control: its opacity (0 to 1), as text.
    /// </summary>
    public const string Opacity = nameof(Opacity);
    /// <summary>
    /// Reported by a text box: its placeholder text.
    /// </summary>
    public const string Placeholder = nameof(Placeholder);
    /// <summary>
    /// Reported by every control: its space-separated style pseudo-classes (eg. ":pointerover").
    /// </summary>
    public const string PseudoClasses = nameof(PseudoClasses);
    /// <summary>
    /// Reported by a selecting items control: its selected item's <see cref="object.ToString"/>, or <see langword="null"/> if none is selected.
    /// </summary>
    public const string SelectedItem = nameof(SelectedItem);
    /// <summary>
    /// Reported by every control: whether its tooltip still shows while it is disabled ("True"/"False").
    /// </summary>
    public const string ShowToolTipOnDisabled = nameof(ShowToolTipOnDisabled);
    /// <summary>
    /// Reported by every control: the text a user sees on it (see <see cref="AutomationTree.TextOf"/>), or <see langword="null"/> if it shows none.
    /// </summary>
    public const string Text = nameof(Text);
    /// <summary>
    /// Reported by every control with text: whether that text is wider than the room it has, ie. clipped or trimmed ("True"/"False"), or <see langword="null"/> for a control without text.
    /// </summary>
    public const string TextOverflows = nameof(TextOverflows);
    /// <summary>
    /// Reported by every control: its tooltip text, or <see langword="null"/> if it has none.
    /// </summary>
    public const string ToolTip = nameof(ToolTip);
    /// <summary>
    /// Reported by every control: its CLR type name.
    /// </summary>
    public const string Type = nameof(Type);
    /// <summary>
    /// Reported by the graph canvas: its viewport's X location in DIP, as text.
    /// </summary>
    public const string ViewportX = nameof(ViewportX);
    /// <summary>
    /// Reported by the graph canvas: its viewport's Y location in DIP, as text.
    /// </summary>
    public const string ViewportY = nameof(ViewportY);
    /// <summary>
    /// Reported by the graph canvas: its current zoom level, as text.
    /// </summary>
    public const string ViewportZoom = nameof(ViewportZoom);
    /// <summary>
    /// Reported by every control: its bounds width in DIP, as text.
    /// </summary>
    public const string Width = nameof(Width);
    /// <summary>
    /// Reported by a window: whether it is the active window ("True"/"False").
    /// </summary>
    public const string WindowIsActive = nameof(WindowIsActive);
    /// <summary>
    /// Reported by a window: its <c>WindowState</c>, as text.
    /// </summary>
    public const string WindowState = nameof(WindowState);
    /// <summary>
    /// Reported by a window: its title.
    /// </summary>
    public const string WindowTitle = nameof(WindowTitle);
    /// <summary>
    /// Reported by a window: its underlying platform window handle (the X11 window id on Linux), as text, or <see langword="null"/> if unavailable.
    /// </summary>
    public const string X11Window = nameof(X11Window);
}
