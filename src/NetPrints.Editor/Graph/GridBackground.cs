using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace NetPrints.Editor.Graph;

/// <summary>How <see cref="GridBackground"/> chooses its render path.</summary>
public enum GridRenderMode
{
    /// <summary>The SkSL shader on a GPU renderer when it compiled, otherwise the CPU path; <c>NETPRINTS_GRID</c> can override this.</summary>
    Auto,

    /// <summary>The SkSL shader, on any renderer (slow on the raster backend); the CPU path when it did not compile.</summary>
    Shader,

    /// <summary>The CPU path.</summary>
    Cpu,
}

/// <summary>The render path that drew a grid frame.</summary>
public enum GridRenderPath
{
    /// <summary>Nothing drawn yet, or the renderer is not Skia.</summary>
    None,

    /// <summary>The frame was drawn with the SkSL shader.</summary>
    Shader,

    /// <summary>The frame was drawn with the CPU path.</summary>
    Cpu,
}

/// <summary>What drew a grid frame, and where: the control's device-pixel origin and device pixels per DIP.</summary>
public sealed record GridRenderInfo(GridRenderPath Path, double OriginX, double OriginY, double Scale);

/// <summary>
/// The infinite background grid of the graph canvas: minor lines every <see cref="CellSize"/>
/// graph units and major lines every <see cref="MajorEvery"/> cells, following
/// <see cref="ViewportLocation"/> and <see cref="ViewportZoom"/>. The grid also paints the canvas
/// background (<see cref="BackgroundColor"/>).
/// </summary>
/// <remarks>
/// <para>
/// Lines are drawn in device pixels, snapped to whole pixels and at least one pixel wide at any
/// display scale; each line is drawn once. Minor lines fade out as the on-screen cell shrinks
/// (<see cref="GridStyle.MinorFadeIn"/> to <see cref="GridStyle.MinorFadeOut"/>).
/// </para>
/// <para>
/// Two render paths share one definition (<see cref="GridStyle"/> and <see cref="GridFrame"/>) and
/// produce identical pixels: an SkSL shader on one rectangle (constant CPU work), used in
/// <see cref="GridRenderMode.Auto"/> only when the renderer has a GPU context and the effect
/// compiled, and a CPU path that fills the visible lines as two paths, used for software and
/// headless rendering or when the shader is unavailable. While <see cref="Mode"/> is
/// <see cref="GridRenderMode.Auto"/>, the environment variable <c>NETPRINTS_GRID</c>
/// (<c>auto</c>, <c>cpu</c> or <c>shader</c>) overrides the choice. See
/// <c>docs/research/2026-09-25-grid-rendering/</c>.
/// </para>
/// <para>
/// The control assumes its ancestors apply only translation and a uniform scale (as in the editor):
/// the device scale is read from the canvas matrix's x scale.
/// </para>
/// </remarks>
public sealed class GridBackground : Control
{
    /// <summary>The environment variable that overrides <see cref="GridRenderMode.Auto"/>.</summary>
    public const string ModeVariable = "NETPRINTS_GRID";

    /// <summary>Registers <see cref="ViewportLocation"/>.</summary>
    public static readonly StyledProperty<Point> ViewportLocationProperty =
        AvaloniaProperty.Register<GridBackground, Point>(nameof(ViewportLocation));

    /// <summary>Registers <see cref="ViewportZoom"/>.</summary>
    public static readonly StyledProperty<double> ViewportZoomProperty =
        AvaloniaProperty.Register<GridBackground, double>(nameof(ViewportZoom), 1.0);

    /// <summary>Registers <see cref="CellSize"/>.</summary>
    public static readonly StyledProperty<double> CellSizeProperty =
        AvaloniaProperty.Register<GridBackground, double>(nameof(CellSize), GraphConstants.GridCellSize);

    /// <summary>Registers <see cref="MajorEvery"/>.</summary>
    public static readonly StyledProperty<int> MajorEveryProperty =
        AvaloniaProperty.Register<GridBackground, int>(nameof(MajorEvery), 8, validate: v => v > 0);

    /// <summary>Registers <see cref="BackgroundColor"/>.</summary>
    public static readonly StyledProperty<Color> BackgroundColorProperty =
        AvaloniaProperty.Register<GridBackground, Color>(nameof(BackgroundColor), Color.FromRgb(0x25, 0x25, 0x25));

    /// <summary>Registers <see cref="MinorColor"/>.</summary>
    public static readonly StyledProperty<Color> MinorColorProperty =
        AvaloniaProperty.Register<GridBackground, Color>(nameof(MinorColor), Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));

    /// <summary>Registers <see cref="MajorColor"/>.</summary>
    public static readonly StyledProperty<Color> MajorColorProperty =
        AvaloniaProperty.Register<GridBackground, Color>(nameof(MajorColor), Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF));

    /// <summary>Registers <see cref="Mode"/>.</summary>
    public static readonly StyledProperty<GridRenderMode> ModeProperty =
        AvaloniaProperty.Register<GridBackground, GridRenderMode>(nameof(Mode));

    private static readonly GridRenderMode EnvironmentMode = ParseMode(Environment.GetEnvironmentVariable(ModeVariable));
    private static int shaderFailureLogged;

    private GridStyle? style;
    private volatile GridRenderInfo? lastFrame;

    static GridBackground()
    {
        AffectsRender<GridBackground>(ViewportLocationProperty, ViewportZoomProperty, CellSizeProperty, MajorEveryProperty,
            BackgroundColorProperty, MinorColorProperty, MajorColorProperty, ModeProperty);
    }

    /// <summary>The graph point shown at the control's top-left corner (Nodify's <c>ViewportLocation</c>).</summary>
    public Point ViewportLocation
    {
        get => GetValue(ViewportLocationProperty);
        set => SetValue(ViewportLocationProperty, value);
    }

    /// <summary>DIPs per graph unit (Nodify's <c>ViewportZoom</c>).</summary>
    public double ViewportZoom
    {
        get => GetValue(ViewportZoomProperty);
        set => SetValue(ViewportZoomProperty, value);
    }

    /// <summary>Minor cell size in graph units.</summary>
    public double CellSize
    {
        get => GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    /// <summary>A major line every this many cells.</summary>
    public int MajorEvery
    {
        get => GetValue(MajorEveryProperty);
        set => SetValue(MajorEveryProperty, value);
    }

    /// <summary>
    /// Canvas background color, painted by the grid (the theme's <c>SystemRegionColor</c>, like the
    /// window region). Always drawn opaque: its alpha is ignored.
    /// </summary>
    public Color BackgroundColor
    {
        get => GetValue(BackgroundColorProperty);
        set => SetValue(BackgroundColorProperty, value);
    }

    /// <summary>Minor line color (usually the <c>GraphGrid.MinorColor</c> theme resource).</summary>
    public Color MinorColor
    {
        get => GetValue(MinorColorProperty);
        set => SetValue(MinorColorProperty, value);
    }

    /// <summary>Major line color (usually the <c>GraphGrid.MajorColor</c> theme resource).</summary>
    public Color MajorColor
    {
        get => GetValue(MajorColorProperty);
        set => SetValue(MajorColorProperty, value);
    }

    /// <summary>How the render path is chosen.</summary>
    public GridRenderMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>The path that drew the latest frame (written on the render thread), for diagnostics and tests.</summary>
    public GridRenderPath LastRenderPath => lastFrame?.Path ?? GridRenderPath.None;

    /// <summary>The latest frame's path, device origin and scale (written on the render thread), for diagnostics and tests.</summary>
    public GridRenderInfo? LastFrame => lastFrame;

    /// <summary>
    /// The mode after the <c>NETPRINTS_GRID</c> override: an explicit <see cref="GridRenderMode.Shader"/>
    /// or <see cref="GridRenderMode.Cpu"/> wins; <see cref="GridRenderMode.Auto"/> takes the environment's mode.
    /// </summary>
    public static GridRenderMode ResolveMode(GridRenderMode mode, GridRenderMode environmentMode) =>
        mode == GridRenderMode.Auto ? environmentMode : mode;

    /// <summary>Parses a <c>NETPRINTS_GRID</c> value (case-insensitive); anything else is <see cref="GridRenderMode.Auto"/>.</summary>
    public static GridRenderMode ParseMode(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "cpu" => GridRenderMode.Cpu,
        "shader" => GridRenderMode.Shader,
        _ => GridRenderMode.Auto,
    };

    /// <summary>Whether a frame is drawn by the shader, given the resolved mode, the renderer and the effect.</summary>
    /// <remarks><paramref name="shaderAvailable"/> is only called when the shader could be used, so <see cref="GridRenderMode.Cpu"/> never compiles it.</remarks>
    public static bool UsesShader(GridRenderMode resolvedMode, bool hasGpuContext, Func<bool> shaderAvailable) => resolvedMode switch
    {
        GridRenderMode.Cpu => false,
        GridRenderMode.Shader => shaderAvailable(),
        _ => hasGpuContext && shaderAvailable(),
    };

    /// <summary>
    /// Queues an immutable draw operation (a <see cref="GridStyle"/> snapshot plus the current
    /// viewport and resolved mode) for the render thread; see <see cref="GridDrawOperation"/>.
    /// </summary>
    /// <param name="context">Drawing context to queue the custom draw operation on.</param>
    public override void Render(DrawingContext context)
    {
        style ??= new GridStyle(CellSize, MajorEvery, MinorWidth: 1, MajorWidth: 1, BackgroundColor, MinorColor, MajorColor);
        context.Custom(new GridDrawOperation(this, new Rect(Bounds.Size), style, ViewportLocation, ViewportZoom,
            ResolveMode(Mode, EnvironmentMode)));
    }

    /// <summary>
    /// Invalidates the cached <see cref="GridStyle"/> when a style-affecting property (cell size,
    /// major-every, or any of the three colors) changes, so <see cref="Render"/> rebuilds it.
    /// </summary>
    /// <param name="change">The property that changed.</param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CellSizeProperty || change.Property == MajorEveryProperty || change.Property == BackgroundColorProperty
            || change.Property == MinorColorProperty || change.Property == MajorColorProperty)
        {
            style = null;
        }
    }

    private static void LogShaderFailureOnce(GridBackground owner)
    {
        if (Interlocked.Exchange(ref shaderFailureLogged, 1) == 0)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.Visual)?.Log(owner,
                "Grid shader unavailable, using the CPU path: {Errors}", GridRenderer.ShaderErrors);
        }
    }

    /// <summary>An immutable snapshot of one grid frame; rendered later, on the render thread.</summary>
    private sealed class GridDrawOperation(GridBackground owner, Rect bounds, GridStyle style, Point location, double zoom,
        GridRenderMode mode) : ICustomDrawOperation
    {
        private readonly (Rect, GridStyle, Point, double, GridRenderMode) key = (bounds, style, location, zoom, mode);

        public Rect Bounds => bounds;

        public bool HitTest(Point p) => false;

        public bool Equals(ICustomDrawOperation? other) => other is GridDrawOperation o && o.key.Equals(key);

        public void Dispose()
        {
        }

        public void Render(ImmediateDrawingContext context)
        {
            if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } feature)
            {
                owner.lastFrame = new GridRenderInfo(GridRenderPath.None, 0, 0, 0);
                return;
            }

            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;
            var matrix = canvas.TotalMatrix;
            var deviceRect = matrix.MapRect(new SKRect(0, 0, (float)bounds.Width, (float)bounds.Height));
            var frame = GridFrame.Compute(style, location.X, location.Y, zoom, matrix.ScaleX, matrix.TransX, matrix.TransY);
            bool shader = UsesShader(mode, lease.GrContext is not null, () => GridRenderer.ShaderAvailable);
            if (!shader && UsesShader(mode, lease.GrContext is not null, () => true))
            {
                LogShaderFailureOnce(owner);
            }

            int saved = canvas.Save();
            try
            {
                canvas.ResetMatrix();
                canvas.ClipRect(deviceRect);
                if (lease.CurrentOpacity < 1)
                {
                    using var layerPaint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(lease.CurrentOpacity * 255)) };
                    canvas.SaveLayer(deviceRect, layerPaint);
                }

                if (!(shader && GridRenderer.DrawShader(canvas, deviceRect, frame)))
                {
                    shader = false;
                    GridRenderer.DrawCpu(canvas, deviceRect, frame);
                }
            }
            finally
            {
                canvas.RestoreToCount(saved);
            }

            owner.lastFrame = new GridRenderInfo(shader ? GridRenderPath.Shader : GridRenderPath.Cpu, matrix.TransX, matrix.TransY, matrix.ScaleX);
        }
    }
}
