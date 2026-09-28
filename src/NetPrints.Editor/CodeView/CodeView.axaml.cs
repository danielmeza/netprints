using System.ComponentModel;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Compilation;
using NetPrints.Editor.Hosting;
using TextMateSharp.Grammars;

namespace NetPrints.Editor.CodeView;

/// <summary>
/// Code-behind of the read-only C# code view (editor-services.md §3): installs TextMate highlighting,
/// Roslyn-driven folding and the diagnostics squiggle renderer, and keeps the TextMate theme in step
/// with the control's actual theme variant. Disposes the TextMate installation and the folding manager
/// when it leaves the visual tree.
/// </summary>
public sealed partial class CodeView : UserControl, IDisposable
{
    private const string CSharpExtension = ".cs";

    private readonly SquiggleRenderer squiggleRenderer = new();
    private readonly FoldingManager foldingManager;
    private RegistryOptions? highlighting;
    private TextMate.Installation? textMate;
    private CodeViewVM? viewModel;
    private bool disposed;

    /// <summary>Loads the control's XAML and installs highlighting, folding and squiggles on <c>Editor</c>.</summary>
    public CodeView()
    {
        InitializeComponent();

        Editor.TextArea.TextView.BackgroundRenderers.Add(squiggleRenderer);
        foldingManager = FoldingManager.Install(Editor.TextArea);
        InstallHighlighting();

        Editor.TextArea.TextView.PointerHover += OnPointerHover;
        Editor.TextArea.TextView.PointerHoverStopped += OnPointerHoverStopped;
        DataContextChanged += OnDataContextChanged;
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
    }

    /// <summary>The bound view model, or <see langword="null"/> if the data context is not one.</summary>
    public CodeViewVM? ViewModel => viewModel;

    /// <summary>The wrapped AvaloniaEdit editor (public for headless UI tests; <c>Editor</c>, the
    /// named element itself, is assembly-internal).</summary>
    public AvaloniaEdit.TextEditor CodeEditor => Editor;

    /// <summary>
    /// Logs faults from hover-triggered quick-info lookups (<see cref="OnPointerHover"/>). Avalonia's
    /// XAML loader constructs this control with no DI hook, and <c>CodeViewVM</c> deliberately has no
    /// <c>EditorContext</c> (FR-038), so the host view sets this from its own <c>EditorContext.LoggerFactory</c>
    /// (<c>ClassInspectorView</c>, the same way <c>GraphEditorView</c> uses <c>graph.Context.LoggerFactory</c>).
    /// Faults are logged only, never shown as an error dialog: a failed quick-info lookup is cosmetic.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; set; }

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

        viewModel = DataContext as CodeViewVM;
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
            case nameof(CodeViewVM.Code):
                RefreshCode();
                break;
            case nameof(CodeViewVM.Diagnostics):
                RefreshDiagnostics();
                break;
            case nameof(CodeViewVM.Foldings):
                RefreshFoldings();
                break;
        }
    }

    private void RefreshCode() => Editor.Text = viewModel?.Code ?? string.Empty;

    private void RefreshDiagnostics()
    {
        squiggleRenderer.Diagnostics = viewModel?.Diagnostics ?? [];
        Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
    }

    private void OnPointerHover(object? sender, PointerEventArgs e)
    {
        if (Editor.GetPositionFromPoint(e.GetPosition(Editor)) is not { } position)
        {
            ToolTip.SetTip(this, null);
            return;
        }

        ILogger logger = LoggerFactory?.CreateLogger<CodeView>() ?? NullLogger<CodeView>.Instance;
        ShowQuickInfoAsync(Editor.Document.GetOffset(position.Location), CancellationToken.None).Forget(logger);
    }

    private void OnPointerHoverStopped(object? sender, PointerEventArgs e) => ToolTip.SetTip(this, null);

    /// <summary>
    /// Shows the signature and summary of the symbol at <paramref name="offset"/> as this control's
    /// tooltip (FR-035, ED-T05; set on the control the automation id is on, not the wrapped
    /// <c>Editor</c>, so the automation tree reports it), or clears it when there is none. Public so
    /// a test can trigger it directly instead of waiting on AvaloniaEdit's own hover delay.
    /// </summary>
    /// <param name="offset">Character offset into the code to look the symbol up at.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    public async Task ShowQuickInfoAsync(int offset, CancellationToken cancellationToken)
    {
        QuickInfo? info = viewModel is null ? null : await viewModel.GetQuickInfoAsync(offset, cancellationToken);
        ToolTip.SetTip(this, info is null ? null : info.Summary is null ? info.Signature : $"{info.Signature}\n{info.Summary}");
    }

    private void RefreshFoldings()
    {
        if (viewModel is null)
        {
            return;
        }

        foldingManager.UpdateFoldings(viewModel.Foldings.Select(folding => new NewFolding(folding.Start, folding.End) { Name = folding.Title }), -1);
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Dispose();
    }

    /// <summary>Disposes the TextMate installation and uninstalls the folding manager.</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (viewModel is not null)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        Editor.TextArea.TextView.PointerHover -= OnPointerHover;
        Editor.TextArea.TextView.PointerHoverStopped -= OnPointerHoverStopped;
        FoldingManager.Uninstall(foldingManager);
        textMate?.Dispose();
    }
}
