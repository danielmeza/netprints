namespace NetPrints.Editor.Hosting.Automation;

/// <summary>
/// Names of the read-only properties the automation tree reports for an element
/// (<see cref="AutomationElement.Properties"/>): the contract between <see cref="AutomationTree"/>
/// and the UI test page objects, so a rename breaks the build instead of a test run.
/// </summary>
public static class AutomationPropertyNames
{
    public const string BackgroundColor = nameof(BackgroundColor);
    public const string Cursor = nameof(Cursor);
    public const string GridCellSize = nameof(GridCellSize);
    public const string GridRenderMode = nameof(GridRenderMode);
    public const string GridRenderPath = nameof(GridRenderPath);
    public const string HasSource = nameof(HasSource);
    public const string Height = nameof(Height);
    public const string IsChecked = nameof(IsChecked);
    public const string IsEnabled = nameof(IsEnabled);
    public const string IsFocused = nameof(IsFocused);
    public const string IsKeyboardFocusWithin = nameof(IsKeyboardFocusWithin);
    public const string IsOpen = nameof(IsOpen);
    public const string IsReadOnly = nameof(IsReadOnly);
    public const string IsSelected = nameof(IsSelected);
    public const string IsVisible = nameof(IsVisible);
    public const string ItemCount = nameof(ItemCount);
    public const string LocationX = nameof(LocationX);
    public const string LocationY = nameof(LocationY);
    public const string MajorColor = nameof(MajorColor);
    public const string MaxViewportZoom = nameof(MaxViewportZoom);
    public const string MinorColor = nameof(MinorColor);
    public const string MinViewportZoom = nameof(MinViewportZoom);
    public const string Opacity = nameof(Opacity);
    public const string Placeholder = nameof(Placeholder);
    public const string PseudoClasses = nameof(PseudoClasses);
    public const string SelectedItem = nameof(SelectedItem);
    public const string ShowToolTipOnDisabled = nameof(ShowToolTipOnDisabled);
    public const string Text = nameof(Text);
    public const string TextOverflows = nameof(TextOverflows);
    public const string ToolTip = nameof(ToolTip);
    public const string Type = nameof(Type);
    public const string ViewportX = nameof(ViewportX);
    public const string ViewportY = nameof(ViewportY);
    public const string ViewportZoom = nameof(ViewportZoom);
    public const string Width = nameof(Width);
    public const string WindowIsActive = nameof(WindowIsActive);
    public const string WindowState = nameof(WindowState);
    public const string WindowTitle = nameof(WindowTitle);
    public const string X11Window = nameof(X11Window);
}
