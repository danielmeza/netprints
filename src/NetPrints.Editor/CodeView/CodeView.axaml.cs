using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
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

        DataContextChanged += OnDataContextChanged;
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
    }

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

        FoldingManager.Uninstall(foldingManager);
        textMate?.Dispose();
    }
}
