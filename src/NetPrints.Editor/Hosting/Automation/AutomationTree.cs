using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using NetPrints.Editor.Graph;
using Nodify.Avalonia;

namespace NetPrints.Editor.Hosting.Automation;

/// <summary>
/// Read-only view of the editor's UI for automation: tracks open windows and finds elements by
/// automation id. Used by the in-app automation agent and by the headless UI test driver, so both
/// see exactly the same elements and properties. Must be used on the UI thread.
/// </summary>
public sealed class AutomationTree : IDisposable
{
    private readonly List<Window> windows = [];
    private readonly Dictionary<Window, string> keys = [];
    private readonly IDisposable openedHandler;
    private readonly IDisposable closedHandler;
    private int nextKey;

    /// <summary>
    /// Starts tracking every window opened from now on.
    /// </summary>
    public AutomationTree()
    {
        openedHandler = Window.WindowOpenedEvent.AddClassHandler(typeof(Window), (sender, _) => Track((Window)sender!));
        closedHandler = Window.WindowClosedEvent.AddClassHandler(typeof(Window), (sender, _) => Untrack((Window)sender!));
    }

    /// <summary>Open windows, in the order they opened.</summary>
    public IReadOnlyList<Window> Windows => windows;

    /// <summary>Starts tracking a window that opened before this tree existed.</summary>
    public void Track(Window window)
    {
        if (!keys.ContainsKey(window))
        {
            keys[window] = "w" + (++nextKey).ToString(CultureInfo.InvariantCulture);
            windows.Add(window);
        }
    }

    private void Untrack(Window window)
    {
        windows.Remove(window);
        keys.Remove(window);
    }

    /// <summary>
    /// The stable key this tree assigned to a tracked window (eg. "w1", "w2", assigned in the order
    /// windows opened).
    /// </summary>
    /// <param name="window">Tracked window to get the key for.</param>
    /// <returns>The window's key.</returns>
    /// <exception cref="KeyNotFoundException"><paramref name="window"/> is not tracked.</exception>
    public string KeyOf(Window window) => keys[window];

    /// <summary>
    /// The tracked window with the given key (see <see cref="KeyOf"/>).
    /// </summary>
    /// <param name="key">Key of the window to find.</param>
    /// <returns>The window with that key.</returns>
    /// <exception cref="InvalidOperationException">No tracked window has that key.</exception>
    public Window WindowByKey(string key) =>
        keys.FirstOrDefault(p => p.Value == key).Key ?? throw new InvalidOperationException($"No open window with key {key}.");

    /// <summary>Finds the elements matching a query in all open windows.</summary>
    public IReadOnlyList<AutomationElement> Find(AutomationQuery query) => FindControls(query).Select(p => Describe(p.Control, p.Window)).ToList();

    /// <summary>Finds the controls matching a query, with the window that owns each one.</summary>
    public IReadOnlyList<(Control Control, Window Window)> FindControls(AutomationQuery query)
    {
        var scopes = query.Within is null
            ? windows.Select(w => ((Control)w, w)).ToList()
            : FindControls(query.Within).Select(p => (p.Control, p.Window)).ToList();

        var result = new List<(Control, Window)>();
        var seen = new HashSet<Control>();
        foreach (var (scope, window) in scopes)
        {
            foreach (var control in SelfAndDescendants(scope))
            {
                if (AutomationProperties.GetAutomationId(control) != query.AutomationId || !seen.Add(control))
                {
                    continue;
                }

                if (!query.IncludeHidden && !control.IsEffectivelyVisible)
                {
                    continue;
                }

                if (query.Name is not null && AutomationProperties.GetName(control) != query.Name)
                {
                    continue;
                }

                if (query.Text is not null && TextOf(control) != query.Text)
                {
                    continue;
                }

                result.Add((control, window));
            }
        }

        if (query.Index is { } index)
        {
            return index >= 0 && index < result.Count ? [result[index]] : [];
        }

        return result;
    }

    private static IEnumerable<Control> SelfAndDescendants(Control root)
    {
        yield return root;
        var visited = new HashSet<Control> { root };
        foreach (var c in root.GetVisualDescendants().OfType<Control>().Concat(root.GetLogicalDescendants().OfType<Control>()))
        {
            if (visited.Add(c))
            {
                yield return c;
            }
        }

        // Open popups (their content is a logical child of the Popup).
        foreach (var popup in visited.OfType<Popup>().Where(p => p.IsOpen && p.Child is Control).ToList())
        {
            foreach (var c in SelfAndDescendants((Control)popup.Child!))
            {
                if (visited.Add(c))
                {
                    yield return c;
                }
            }
        }
    }

    /// <summary>Describes one control relative to its owning window.</summary>
    public AutomationElement Describe(Control control, Window window)
    {
        var size = control.Bounds.Size;
        var origin = control == window ? new Point(0, 0) : control.TranslatePoint(new Point(0, 0), window);

        // Controls in popup windows (desktop) are not in the window's visual tree: use their own top level.
        var topLevel = TopLevel.GetTopLevel(control);
        origin ??= topLevel is not null ? control.TranslatePoint(new Point(0, 0), topLevel) : null;
        var client = new AutomationRect(origin?.X ?? 0, origin?.Y ?? 0, size.Width, size.Height);

        AutomationRect screen = client;
        if (topLevel is not null && control.TranslatePoint(new Point(0, 0), topLevel) is { } inTopLevel)
        {
            var topLeft = topLevel.PointToScreen(inTopLevel);
            double scaling = topLevel.RenderScaling;
            screen = new AutomationRect(topLeft.X, topLeft.Y, size.Width * scaling, size.Height * scaling);
        }

        return new AutomationElement(
            AutomationProperties.GetAutomationId(control) ?? "",
            AutomationProperties.GetName(control),
            keys.TryGetValue(window, out var key) ? key : "",
            client,
            screen,
            Properties(control, window));
    }

    private static IReadOnlyDictionary<string, string?> Properties(Control control, Window window)
    {
        var p = new Dictionary<string, string?>
        {
            [AutomationPropertyNames.Type] = control.GetType().Name,
            [AutomationPropertyNames.Text] = TextOf(control),
            [AutomationPropertyNames.IsEnabled] = control.IsEffectivelyEnabled.ToString(),
            [AutomationPropertyNames.IsVisible] = control.IsEffectivelyVisible.ToString(),
            [AutomationPropertyNames.IsFocused] = control.IsFocused.ToString(),
            [AutomationPropertyNames.IsKeyboardFocusWithin] = control.IsKeyboardFocusWithin.ToString(),
            [AutomationPropertyNames.PseudoClasses] = string.Join(' ', control.Classes),
            [AutomationPropertyNames.ToolTip] = ToolTip.GetTip(control) as string,
            [AutomationPropertyNames.ShowToolTipOnDisabled] = ToolTip.GetShowOnDisabled(control).ToString(),
            [AutomationPropertyNames.Cursor] = EditorCursors.NameOf(control.Cursor),
            [AutomationPropertyNames.TextOverflows] = TextOverflows(control)?.ToString(),
            [AutomationPropertyNames.Opacity] = Invariant(control.Opacity),
            [AutomationPropertyNames.Width] = Invariant(control.Bounds.Width),
            [AutomationPropertyNames.Height] = Invariant(control.Bounds.Height),
            [AutomationPropertyNames.WindowTitle] = window.Title,
            [AutomationPropertyNames.WindowState] = window.WindowState.ToString(),
            [AutomationPropertyNames.WindowIsActive] = window.IsActive.ToString(),
            [AutomationPropertyNames.X11Window] = window.TryGetPlatformHandle()?.Handle.ToString(CultureInfo.InvariantCulture),
        };

        switch (control)
        {
            case ToggleButton toggle:
                p[AutomationPropertyNames.IsChecked] = toggle.IsChecked?.ToString();
                break;
            case TextBox textBox:
                p[AutomationPropertyNames.IsReadOnly] = textBox.IsReadOnly.ToString();
                p[AutomationPropertyNames.Placeholder] = textBox.PlaceholderText;
                break;
            case NodifyEditor editor:
                p[AutomationPropertyNames.ViewportZoom] = Invariant(editor.ViewportZoom);
                p[AutomationPropertyNames.ViewportX] = Invariant(editor.ViewportLocation.X);
                p[AutomationPropertyNames.ViewportY] = Invariant(editor.ViewportLocation.Y);
                p[AutomationPropertyNames.MinViewportZoom] = Invariant(editor.MinViewportZoom);
                p[AutomationPropertyNames.MaxViewportZoom] = Invariant(editor.MaxViewportZoom);
                p[AutomationPropertyNames.GridCellSize] = Invariant(editor.GridCellSize);
                break;
            case GridBackground grid:
                p[AutomationPropertyNames.GridRenderPath] = grid.LastRenderPath.ToString();
                p[AutomationPropertyNames.GridRenderMode] = grid.Mode.ToString();
                p[AutomationPropertyNames.ViewportZoom] = Invariant(grid.ViewportZoom);
                p[AutomationPropertyNames.ViewportX] = Invariant(grid.ViewportLocation.X);
                p[AutomationPropertyNames.ViewportY] = Invariant(grid.ViewportLocation.Y);
                p[AutomationPropertyNames.BackgroundColor] = grid.BackgroundColor.ToString();
                p[AutomationPropertyNames.MinorColor] = grid.MinorColor.ToString();
                p[AutomationPropertyNames.MajorColor] = grid.MajorColor.ToString();
                break;
            case Image image:
                p[AutomationPropertyNames.HasSource] = (image.Source is not null).ToString();
                break;
            case Popup popup:
                p[AutomationPropertyNames.IsOpen] = popup.IsOpen.ToString();
                break;
            case SelectingItemsControl selecting:
                p[AutomationPropertyNames.SelectedItem] = selecting.SelectedItem?.ToString();
                p[AutomationPropertyNames.ItemCount] = selecting.ItemCount.ToString(CultureInfo.InvariantCulture);
                break;
        }

        if (control.FindAncestorOfType<ItemContainer>(includeSelf: true) is { } container)
        {
            p[AutomationPropertyNames.IsSelected] = container.IsSelected.ToString();
            p[AutomationPropertyNames.LocationX] = Invariant(container.Location.X);
            p[AutomationPropertyNames.LocationY] = Invariant(container.Location.Y);
        }

        return p;
    }

    /// <summary>
    /// Whether the text a control shows is wider than the room it has (clipped or trimmed); null
    /// for controls without text.
    /// </summary>
    private static bool? TextOverflows(Control control)
    {
        var textBlock = control as TextBlock
            ?? (control is ContentControl { Content: string } ? control.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault() : null);
        if (textBlock is null || string.IsNullOrEmpty(textBlock.Text))
        {
            return null;
        }

        // Natural width of the text on one line, against the width it was given.
        var natural = new TextBlock
        {
            Text = textBlock.Text,
            FontFamily = textBlock.FontFamily,
            FontSize = textBlock.FontSize,
            FontWeight = textBlock.FontWeight,
            FontStyle = textBlock.FontStyle,
            FontStretch = textBlock.FontStretch,
        };
        natural.Measure(Size.Infinity);
        return natural.DesiredSize.Width > textBlock.Bounds.Width + 0.5;
    }

    private static string Invariant(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>The text a user sees on a control.</summary>
    public static string? TextOf(Control control) => control switch
    {
        Window window => window.Title,
        TextBlock textBlock => textBlock.Text,
        TextBox textBox => textBox.Text,
        AutoCompleteBox autoComplete => autoComplete.Text,
        ComboBox comboBox => comboBox.SelectedItem?.ToString(),
        ContentControl { Content: string text } => text,
        _ => null,
    };

    /// <summary>A text dump of every tracked window's elements with automation ids (diagnostics).</summary>
    public string Dump()
    {
        var sb = new StringBuilder();
        foreach (var window in windows)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"[{KeyOf(window)}] {window.GetType().Name} '{window.Title}' {window.WindowState} {window.Bounds.Size}");
            foreach (var control in SelfAndDescendants(window).Where(c => !string.IsNullOrEmpty(AutomationProperties.GetAutomationId(c))))
            {
                var e = Describe(control, window);
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"  {e.AutomationId}{(e.Name is null ? "" : $"[{e.Name}]")} {e[AutomationPropertyNames.Type]} text='{e.Text}' visible={e.IsVisible} enabled={e.IsEnabled} bounds=({e.Bounds.X:0},{e.Bounds.Y:0},{e.Bounds.Width:0},{e.Bounds.Height:0}) {e[AutomationPropertyNames.PseudoClasses]}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Stops tracking window open/close events. Already-tracked windows are not untracked.
    /// </summary>
    public void Dispose()
    {
        openedHandler.Dispose();
        closedHandler.Dispose();
    }
}
