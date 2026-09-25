using Avalonia.Media;
using SkiaSharp;

namespace NetPrints.Editor.Graph;

/// <summary>
/// The two render paths of the canvas grid. Both draw a <see cref="GridFrame"/> in device pixels
/// (the canvas matrix reset), snap every line with <see cref="GridFrame.SnapStart"/>, and write the
/// frame's final colors (major lines win at crossings) without blending, so their output is
/// pixel-identical.
/// </summary>
internal static class GridRenderer
{

    private static readonly Lazy<(SKRuntimeEffect? Effect, string? Errors)> Effect = new(() => Compile(SKRuntimeEffect.CreateShader));

    /// <summary>Compiles an SkSL shader source (<see cref="SKRuntimeEffect.CreateShader"/> by default).</summary>
    internal delegate SKRuntimeEffect? EffectCompiler(string source, out string errors);

    /// <summary>
    /// Compiles the grid shader; null with the errors when it does not compile.
    /// </summary>
    /// <remarks>
    /// Any exception (for example a native libSkiaSharp that does not match the managed package)
    /// also means "unavailable", so the CPU path takes over instead of the exception being cached
    /// and rethrown on every frame.
    /// </remarks>
    internal static (SKRuntimeEffect? Effect, string? Errors) Compile(EffectCompiler compiler)
    {
        try
        {
            var effect = compiler(LoadShaderSource(), out string errors);
            return (effect, effect is null ? errors : null);
        }
        catch (Exception e)
        {
            return (null, e.ToString());
        }
    }

    private static string LoadShaderSource()
    {
        using var stream = typeof(GridRenderer).Assembly.GetManifestResourceStream("NetPrints.Editor.Graph.GridShader.sksl")
            ?? throw new InvalidOperationException("The grid shader resource is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [ThreadStatic] private static SKPath? minorPath;
    [ThreadStatic] private static SKPath? majorPath;
    [ThreadStatic] private static SKPaint? fillPaint;
    [ThreadStatic] private static SKPaint? shaderPaint;
    [ThreadStatic] private static SKRuntimeEffectUniforms? shaderUniforms;

    /// <summary>Whether the SkSL effect compiled; compiled once, on first use.</summary>
    public static bool ShaderAvailable => Effect.Value.Effect is not null;

    /// <summary>The compiler errors when <see cref="ShaderAvailable"/> is false.</summary>
    public static string? ShaderErrors => Effect.Value.Errors;

    /// <summary>
    /// Draws the grid with the SkSL shader on one rectangle: constant CPU work per frame (the paint
    /// and uniforms are reused per render thread; only the shader object is per frame). Returns
    /// false (drawing nothing) when the effect is unavailable, so the caller can fall back.
    /// </summary>
    /// <remarks>Only fast on a GPU canvas; on the raster backend it is about 100 times slower than <see cref="DrawCpu"/>.</remarks>
    public static bool DrawShader(SKCanvas canvas, SKRect deviceRect, in GridFrame frame)
    {
        if (Effect.Value.Effect is not { } effect)
        {
            return false;
        }

        var uniforms = shaderUniforms ??= new SKRuntimeEffectUniforms(effect);
        uniforms["phase"] = new SKPoint(frame.PhaseX, frame.PhaseY);
        uniforms["cell"] = frame.Cell;
        uniforms["majorEvery"] = (float)frame.MajorEvery;
        uniforms["minorWidth"] = (float)frame.MinorWidth;
        uniforms["majorWidth"] = (float)frame.MajorWidth;
        uniforms["backgroundColor"] = Premultiplied(frame.BackgroundColor);
        uniforms["minorColor"] = frame.MinorAlpha == 0 ? default : Premultiplied(frame.MinorColor);
        uniforms["majorColor"] = Premultiplied(frame.MajorColor);
        using var shader = effect.ToShader(uniforms);
        if (shader is null)
        {
            return false;
        }

        var paint = shaderPaint ??= new SKPaint { IsAntialias = false, BlendMode = SKBlendMode.Src };
        paint.Shader = shader;
        canvas.DrawRect(deviceRect, paint);
        paint.Shader = null;
        return true;
    }

    /// <summary>
    /// Draws the grid as whole-pixel rectangles: the background, then the visible minor lines as one
    /// path and the major lines as another, each filled once with the frame's final color (the
    /// non-zero fill takes the union of the rectangles). The paths and paint are reused per render
    /// thread, without managed allocations.
    /// </summary>
    public static void DrawCpu(SKCanvas canvas, SKRect deviceRect, in GridFrame frame)
    {
        var minor = minorPath ??= new SKPath();
        var major = majorPath ??= new SKPath();
        minor.Rewind();
        major.Rewind();
        AddLines(minor, major, frame, deviceRect, vertical: true);
        AddLines(minor, major, frame, deviceRect, vertical: false);

        var paint = fillPaint ??= new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill, BlendMode = SKBlendMode.Src };
        paint.Color = ToSk(frame.BackgroundColor);
        canvas.DrawRect(deviceRect, paint);
        if (frame.MinorAlpha > 0)
        {
            paint.Color = ToSk(frame.MinorColor);
            canvas.DrawPath(minor, paint);
        }

        paint.Color = ToSk(frame.MajorColor);
        canvas.DrawPath(major, paint);
    }

    private static void AddLines(SKPath minor, SKPath major, in GridFrame frame, SKRect rect, bool vertical)
    {
        float from = vertical ? rect.Left : rect.Top;
        float to = vertical ? rect.Right : rect.Bottom;
        float phase = vertical ? frame.PhaseX : frame.PhaseY;
        int first = (int)MathF.Floor((from - phase) / frame.Cell) - 1;
        int last = (int)MathF.Ceiling((to - phase) / frame.Cell) + 1;
        for (int k = first; k <= last; k++)
        {
            bool isMajor = frame.IsMajor(k);
            if (!isMajor && frame.MinorAlpha == 0)
            {
                continue;
            }

            int width = isMajor ? frame.MajorWidth : frame.MinorWidth;
            float start = GridFrame.SnapStart(phase + k * frame.Cell, width);
            (isMajor ? major : minor).AddRect(vertical
                ? new SKRect(start, rect.Top, start + width, rect.Bottom)
                : new SKRect(rect.Left, start, rect.Right, start + width));
        }
    }

    private static SKColor ToSk(Color color) => new(color.R, color.G, color.B, color.A);

    private static SKColorF Premultiplied(Color color)
    {
        float a = color.A / 255f;
        return new SKColorF(color.R / 255f * a, color.G / 255f * a, color.B / 255f * a, a);
    }
}
