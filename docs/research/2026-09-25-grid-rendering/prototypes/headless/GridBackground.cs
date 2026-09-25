#nullable enable
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

public enum GridRenderMode { Auto, Shader, Cpu }

public sealed class GridBackground : Control
{
    public static readonly StyledProperty<Point> ViewportLocationProperty = AvaloniaProperty.Register<GridBackground, Point>(nameof(ViewportLocation));
    public static readonly StyledProperty<double> ViewportZoomProperty = AvaloniaProperty.Register<GridBackground, double>(nameof(ViewportZoom), 1.0);
    public static readonly StyledProperty<Color> MinorColorProperty = AvaloniaProperty.Register<GridBackground, Color>(nameof(MinorColor), Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));
    public static readonly StyledProperty<Color> MajorColorProperty = AvaloniaProperty.Register<GridBackground, Color>(nameof(MajorColor), Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF));
    public static readonly StyledProperty<GridRenderMode> ModeProperty = AvaloniaProperty.Register<GridBackground, GridRenderMode>(nameof(Mode));

    static GridBackground() => AffectsRender<GridBackground>(ViewportLocationProperty, ViewportZoomProperty, MinorColorProperty, MajorColorProperty, ModeProperty);

    public Point ViewportLocation { get => GetValue(ViewportLocationProperty); set => SetValue(ViewportLocationProperty, value); }
    public double ViewportZoom { get => GetValue(ViewportZoomProperty); set => SetValue(ViewportZoomProperty, value); }
    public Color MinorColor { get => GetValue(MinorColorProperty); set => SetValue(MinorColorProperty, value); }
    public Color MajorColor { get => GetValue(MajorColorProperty); set => SetValue(MajorColorProperty, value); }
    public GridRenderMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    /// For diagnostics/tests: which path the last frame used.
    public static volatile string? LastPath;

    public override void Render(DrawingContext context)
    {
        var s = new GridStyle(MinorColor: ToSk(MinorColor), MajorColor: ToSk(MajorColor));
        context.Custom(new GridDrawOp(new Rect(Bounds.Size), s, ViewportLocation, ViewportZoom, Mode));
    }

    static SKColor ToSk(Color c) => new(c.R, c.G, c.B, c.A);

    // Immutable snapshot: Render() runs on the render thread, later than Control.Render().
    sealed class GridDrawOp(Rect bounds, GridStyle style, Point loc, double zoom, GridRenderMode mode) : ICustomDrawOperation
    {
        readonly (Rect, GridStyle, Point, double, GridRenderMode) key = (bounds, style, loc, zoom, mode);
        public Rect Bounds => bounds;
        public bool HitTest(Point p) => false;
        public bool Equals(ICustomDrawOperation? other) => other is GridDrawOp o && o.key.Equals(key);
        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } feature) { LastPath = "none"; return; } // non-Skia backend
            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;
            var m = canvas.TotalMatrix;                                   // includes RenderScaling and this control's offset
            var deviceRect = m.MapRect(new SKRect(0, 0, (float)bounds.Width, (float)bounds.Height));
            var frame = GridFrame.Compute(style, loc.X, loc.Y, zoom, m.ScaleX, m.TransX, m.TransY, lease.CurrentOpacity);

            bool useShader = mode switch
            {
                GridRenderMode.Shader => GridRenderer.ShaderAvailable,
                GridRenderMode.Cpu => false,
                _ => lease.GrContext is not null && GridRenderer.ShaderAvailable, // GPU only: SkSL on the raster backend is ~100x slower
            };

            canvas.Save();
            canvas.ResetMatrix();                                         // draw in device pixels; clip stays in device space
            canvas.ClipRect(deviceRect);
            if (useShader) GridRenderer.DrawShader(canvas, deviceRect, frame); else GridRenderer.DrawCpu(canvas, deviceRect, frame);
            canvas.Restore();
            LastPath = useShader ? "shader" : "cpu";
        }
    }
}
