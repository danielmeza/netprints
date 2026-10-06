using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Shell;
using Nodify.Avalonia;

namespace NetPrints.Editor.Behaviors;

/// <summary>
/// Keeps the viewport of the Nodify editor below the attached control and the viewport of a document the same: the document's
/// location and zoom are put on the canvas when it is shown (a restored session, or a tab shown again), and the canvas's pans and
/// zooms are written back to the document, which is what a session saves. A document still at the default viewport takes the
/// canvas's instead, so a view the canvas already moved (revealing a node) is kept.
/// </summary>
public sealed class GraphViewportBehavior : StyledElementBehavior<Control>
{
    /// <summary>Registers <see cref="Document"/>.</summary>
    public static readonly StyledProperty<IViewportDocument?> DocumentProperty =
        AvaloniaProperty.Register<GraphViewportBehavior, IViewportDocument?>(nameof(Document));

    private NodifyEditor? editor;
    private IViewportDocument? followed;
    private bool syncing;

    /// <summary>Gets or sets the document whose viewport the canvas shows.</summary>
    public IViewportDocument? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is { } control)
        {
            control.Loaded += OnLoaded;
            control.Unloaded += OnUnloaded;
            if (control.IsLoaded)
            {
                Hook();
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnDetaching()
    {
        if (AssociatedObject is { } control)
        {
            control.Loaded -= OnLoaded;
            control.Unloaded -= OnUnloaded;
        }

        Unhook();
        base.OnDetaching();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DocumentProperty && AssociatedObject is { IsLoaded: true })
        {
            Hook();
        }
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Hook();

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Unhook();

    private void Hook()
    {
        Unhook();
        editor = AssociatedObject?.GetVisualDescendants().OfType<NodifyEditor>().FirstOrDefault();
        followed = Document;
        if (editor is null || followed is null)
        {
            return;
        }

        if (followed.ViewportLocation == GraphPoint.Zero && followed.ViewportZoom == 1)
        {
            Pull();
        }
        else
        {
            Push();
        }

        editor.PropertyChanged += OnEditorChanged;
        if (followed is INotifyPropertyChanged notifying)
        {
            notifying.PropertyChanged += OnDocumentChanged;
        }
    }

    private void Unhook()
    {
        if (editor is not null)
        {
            editor.PropertyChanged -= OnEditorChanged;
        }

        if (followed is INotifyPropertyChanged notifying)
        {
            notifying.PropertyChanged -= OnDocumentChanged;
        }

        editor = null;
        followed = null;
    }

    private void OnEditorChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == NodifyEditor.ViewportLocationProperty || e.Property == NodifyEditor.ViewportZoomProperty)
        {
            Pull();
        }
    }

    private void OnDocumentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IViewportDocument.ViewportLocation) or nameof(IViewportDocument.ViewportZoom))
        {
            Push();
        }
    }

    private void Push()
    {
        if (syncing || editor is null || followed is null)
        {
            return;
        }

        syncing = true;
        try
        {
            editor.ViewportZoom = followed.ViewportZoom;
            editor.ViewportLocation = new Point(followed.ViewportLocation.X, followed.ViewportLocation.Y);
            followed.ViewportZoom = editor.ViewportZoom;
            followed.ViewportLocation = new GraphPoint(editor.ViewportLocation.X, editor.ViewportLocation.Y);
        }
        finally
        {
            syncing = false;
        }
    }

    private void Pull()
    {
        if (syncing || editor is null || followed is null)
        {
            return;
        }

        syncing = true;
        try
        {
            followed.ViewportZoom = editor.ViewportZoom;
            followed.ViewportLocation = new GraphPoint(editor.ViewportLocation.X, editor.ViewportLocation.Y);
        }
        finally
        {
            syncing = false;
        }
    }
}
