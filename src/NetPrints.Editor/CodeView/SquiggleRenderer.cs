using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using NetPrints.Compilation;

namespace NetPrints.Editor.CodeView;

/// <summary>
/// Draws a wavy underline under each diagnostic's span (editor-services.md §3: AvaloniaEdit ships no
/// text-marker service, research.md R2), colored by <see cref="CodeDiagnostic.Severity"/>. The view
/// sets <see cref="Diagnostics"/> and redraws the layer whenever <c>CodeViewVM.Diagnostics</c> changes.
/// </summary>
public sealed class SquiggleRenderer : IBackgroundRenderer
{
    private const double AmplitudeInDeviceIndependentPixels = 1.5;
    private const double WavelengthInDeviceIndependentPixels = 4;

    private static readonly IPen ErrorPen = new Pen(Brushes.Red, 1);
    private static readonly IPen WarningPen = new Pen(Brushes.DarkOrange, 1);
    private static readonly IPen InfoPen = new Pen(Brushes.DodgerBlue, 1);

    /// <summary>Diagnostics to draw squiggles for; set by the view whenever the code view's diagnostics change.</summary>
    public IReadOnlyList<CodeDiagnostic> Diagnostics { get; set; } = [];

    /// <inheritdoc/>
    public KnownLayer Layer => KnownLayer.Selection;

    /// <inheritdoc/>
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(textView);
        ArgumentNullException.ThrowIfNull(drawingContext);

        if (textView.Document is not { } document || Diagnostics.Count == 0)
        {
            return;
        }

        textView.EnsureVisualLines();
        foreach (CodeDiagnostic diagnostic in Diagnostics)
        {
            if (TryGetSegment(document, diagnostic, out DiagnosticSegment segment))
            {
                IPen pen = PenFor(diagnostic.Severity);
                foreach (Rect rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                {
                    DrawSquiggle(drawingContext, pen, rect);
                }
            }
        }
    }

    private static bool TryGetSegment(TextDocument document, CodeDiagnostic diagnostic, out DiagnosticSegment segment)
    {
        segment = default;
        if (diagnostic.Span is not { } span || span.Start.Line < 0 || span.End.Line >= document.LineCount)
        {
            return false;
        }

        int start = document.GetOffset(span.Start.Line + 1, span.Start.Character + 1);
        int end = document.GetOffset(span.End.Line + 1, span.End.Character + 1);
        if (end <= start)
        {
            return false;
        }

        segment = new DiagnosticSegment(start, end - start);
        return true;
    }

    private static IPen PenFor(CodeDiagnosticSeverity severity) => severity switch
    {
        CodeDiagnosticSeverity.Error => ErrorPen,
        CodeDiagnosticSeverity.Warning => WarningPen,
        _ => InfoPen,
    };

    private static void DrawSquiggle(DrawingContext drawingContext, IPen pen, Rect rect)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(new Point(rect.Left, rect.Bottom), isFilled: false);
            bool crest = true;
            for (double x = rect.Left; x < rect.Right; x += WavelengthInDeviceIndependentPixels)
            {
                double y = rect.Bottom - (crest ? AmplitudeInDeviceIndependentPixels : 0);
                context.LineTo(new Point(x, y));
                crest = !crest;
            }

            context.EndFigure(isClosed: false);
        }

        drawingContext.DrawGeometry(null, pen, geometry);
    }

    /// <summary>A diagnostic's span translated into document offsets, for <see cref="BackgroundGeometryBuilder"/>.</summary>
    private readonly record struct DiagnosticSegment(int Offset, int Length) : ISegment
    {
        public int EndOffset => Offset + Length;
    }
}
