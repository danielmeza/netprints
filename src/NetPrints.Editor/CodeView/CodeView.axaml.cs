using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using NetPrints.Compilation;
using TextMateSharp.Grammars;

namespace NetPrints.Editor.CodeView;

/// <summary>
/// Code-behind of the read-only C# code view (editor-services.md §3): installs TextMate highlighting,
/// Roslyn-driven folding and the pointer-hover handlers while attached to the visual tree, and keeps
/// the TextMate theme in step with the control's actual theme variant. Installs and uninstalls
/// symmetrically on attach/detach (R2-13), instead of tearing them down for good on first detach, so a
/// re-attach (re-templating, moving the control into a tab or dock) still highlights, folds and hovers.
/// </summary>
public sealed partial class CodeView : UserControl, IDisposable
{
    private const string CSharpExtension = ".cs";

    private readonly SquiggleRenderer squiggleRenderer = new();
    private FoldingManager? foldingManager;
    private RegistryOptions? highlighting;
    private TextMate.Installation? textMate;
    private CodeViewViewModel? viewModel;

    /// <summary>Loads the control's XAML and hooks the data-context and theme-change handlers,
    /// which do not depend on this control being attached to the visual tree.</summary>
    public CodeView()
    {
        InitializeComponent();

        Editor.TextArea.TextView.BackgroundRenderers.Add(squiggleRenderer);
        DataContextChanged += OnDataContextChanged;
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
    }

    /// <summary>The bound view model, or <see langword="null"/> if the data context is not one.</summary>
    public CodeViewViewModel? ViewModel => viewModel;

    /// <summary>The wrapped AvaloniaEdit editor (public for headless UI tests; <c>Editor</c>, the
    /// named element itself, is assembly-internal).</summary>
    public AvaloniaEdit.TextEditor CodeEditor => Editor;

    private void InstallHighlighting()
    {
        try
        {
            var options = new RegistryOptions(ThemeFor(ActualThemeVariant));
            TextMate.Installation installation = Editor.InstallTextMate(options);
            installation.SetGrammar(options.GetScopeByLanguageId(options.GetLanguageByExtension(CSharpExtension).Id));
            highlighting = options;
            textMate?.Dispose();
            textMate = installation;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // TextMateSharp's native Oniguruma dependency is not guaranteed everywhere (research.md R2,
            // risk K6: the P5 browser build): keep Editor as a plain, uncolored TextEditor instead.
            highlighting = null;
            textMate?.Dispose();
            textMate = null;
        }
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        if (highlighting is not { } options || textMate is not { } installation)
        {
            return;
        }

        installation.SetTheme(options.LoadTheme(ThemeFor(ActualThemeVariant)));
    }

    private static ThemeName ThemeFor(ThemeVariant variant) => variant == ThemeVariant.Light ? ThemeName.LightPlus : ThemeName.DarkPlus;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (viewModel is not null)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        viewModel = DataContext as CodeViewViewModel;
        if (viewModel is null)
        {
            return;
        }

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        RefreshCode();
        RefreshDiagnostics();
        RefreshFoldings();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CodeViewViewModel.Code):
                RefreshCode();
                break;
            case nameof(CodeViewViewModel.Diagnostics):
                RefreshDiagnostics();
                break;
            case nameof(CodeViewViewModel.Foldings):
                RefreshFoldings();
                break;
        }
    }

    private void RefreshCode()
    {
        Editor.Text = viewModel?.Code ?? string.Empty;
        viewModel?.ClearQuickInfoCommand.Execute(null); // OWN-01: stale hover content would otherwise linger over the new text.
    }

    private void RefreshDiagnostics()
    {
        squiggleRenderer.Diagnostics = viewModel?.Diagnostics ?? [];
        Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
    }

    private void OnPointerHover(object? sender, PointerEventArgs e)
    {
        if (Editor.GetPositionFromPoint(e.GetPosition(Editor)) is not { } position)
        {
            viewModel?.ClearQuickInfoCommand.Execute(null);
            return;
        }

        viewModel?.ShowQuickInfoCommand.Execute(Editor.Document.GetOffset(position.Location));
    }

    private void OnPointerHoverStopped(object? sender, PointerEventArgs e) => viewModel?.ClearQuickInfoCommand.Execute(null);

    private void OnPointerExited(object? sender, PointerEventArgs e) => viewModel?.ClearQuickInfoCommand.Execute(null);

    private void RefreshFoldings()
    {
        if (viewModel is null || foldingManager is not { } manager)
        {
            return;
        }

        manager.UpdateFoldings(viewModel.Foldings.Select(folding => new NewFolding(folding.Start, folding.End) { Name = folding.Title }), -1);
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        foldingManager = FoldingManager.Install(Editor.TextArea);
        InstallHighlighting();
        Editor.TextArea.TextView.PointerHover += OnPointerHover;
        Editor.TextArea.TextView.PointerHoverStopped += OnPointerHoverStopped;
        PointerExited += OnPointerExited;

        RefreshFoldings(); // The view model may already have foldings from before this attach.
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UninstallEditingSupport();
    }

    /// <summary>Uninstalls folding, TextMate and the pointer-hover handlers (symmetric with <see
    /// cref="OnAttachedToVisualTree"/>). Called on detach, and again (harmlessly, everything is already
    /// uninstalled) from <see cref="Dispose"/>.</summary>
    private void UninstallEditingSupport()
    {
        Editor.TextArea.TextView.PointerHover -= OnPointerHover;
        Editor.TextArea.TextView.PointerHoverStopped -= OnPointerHoverStopped;
        PointerExited -= OnPointerExited;
        viewModel?.ClearQuickInfoCommand.Execute(null);

        if (foldingManager is { } manager)
        {
            FoldingManager.Uninstall(manager);
            foldingManager = null;
        }

        textMate?.Dispose();
        textMate = null;
        highlighting = null;
    }

    /// <summary>
    /// Uninstalls folding/TextMate/hover (if still attached) and unsubscribes from the data-context and
    /// theme-change events. Unlike <see cref="OnDetachedFromVisualTree"/>, this is for real: nothing
    /// here reinstalls on a later attach. No owner calls this today (<c>ClassInspectorView</c> is torn
    /// down with its window); it exists so <see cref="textMate"/>'s ownership is explicit (IDISP006)
    /// and so a future owner that reuses this control across windows has a clean way to retire it.
    /// </summary>
    public void Dispose()
    {
        UninstallEditingSupport();

        if (viewModel is not null)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel = null;
        }

        DataContextChanged -= OnDataContextChanged;
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
    }
}
