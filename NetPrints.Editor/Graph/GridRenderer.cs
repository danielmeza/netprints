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
    // p is the device pixel centre; hits() returns (minor, major) coverage along one axis.
    private const string ShaderSource = """
        uniform float2 phase;
        uniform float cell;
        uniform float majorEvery;
        uniform float minorWidth;
        uniform float majorWidth;
        uniform float4 backgroundColor;
        uniform float4 minorColor;
        uniform float4 majorColor;

        float2 hits(float c, float o) {
            float column = floor(c);
            float k = floor((c - o) / cell + 0.5);
            float m = k - majorEvery * floor(k / majorEvery);
            bool isMajor = m < 0.5 || m > majorEvery - 0.5;
            float w = isMajor ? majorWidth : minorWidth;
            float start = floor(o + k * cell + 0.5 - 0.5 * w);
            float hit = (column >= start && column < start + w) ? 1.0 : 0.0;
            return isMajor ? float2(0.0, hit) : float2(hit, 0.0);
        }

        half4 main(float2 p) {
            float2 hx = hits(p.x, phase.x);
            float2 hy = hits(p.y, phase.y);
            if (max(hx.y, hy.y) > 0.5) { return half4(majorColor); }
            if (minorColor.a > 0.0 && max(hx.x, hy.x) > 0.5) { return half4(minorColor); }
            return half4(backgroundColor);
        }
        """;

    private static readonly Lazy<(SKRuntimeEffect? Effect, string? Errors)> Effect = new(() =>
    {
        var effect = SKRuntimeEffect.CreateShader(ShaderSource, out string errors);
        return (effect, effect is null ? errors : null);
    });

    [ThreadStatic] private static SKPath? minorPath;
    [ThreadStatic] private static SKPath? majorPath;
    [ThreadStatic] private static SKPaint? fillPaint;

    /// <summary>Whether the SkSL effect compiled; compiled once, on first use.</summary>
    public static bool ShaderAvailable => Effect.Value.Effect is not null;

    /// <summary>The compiler errors when <see cref="ShaderAvailable"/> is false.</summary>
    public static string? ShaderErrors => Effect.Value.Errors;

    /// <summary>
    /// Draws the grid with the SkSL shader on one rectangle: constant CPU work per frame. Returns
    /// false (drawing nothing) when the effect is unavailable, so the caller can fall back.
    /// </summary>
    /// <remarks>Only fast on a GPU canvas; on the raster backend it is about 100 times slower than <see cref="DrawCpu"/>.</remarks>
    public static bool DrawShader(SKCanvas canvas, SKRect deviceRect, in GridFrame frame)
    {
        if (Effect.Value.Effect is not { } effect)
        {
            return false;
        }

        using var uniforms = new SKRuntimeEffectUniforms(effect)
        {
            ["phase"] = new SKPoint(frame.PhaseX, frame.PhaseY),
            ["cell"] = frame.Cell,
            ["majorEvery"] = (float)frame.MajorEvery,
            ["minorWidth"] = (float)frame.MinorWidth,
            ["majorWidth"] = (float)frame.MajorWidth,
            ["backgroundColor"] = Premultiplied(frame.BackgroundColor),
            ["minorColor"] = frame.MinorAlpha == 0 ? default : Premultiplied(frame.MinorColor),
            ["majorColor"] = Premultiplied(frame.MajorColor),
        };
        using var shader = effect.ToShader(uniforms);
        if (shader is null)
        {
            return false;
        }

        using var paint = new SKPaint { Shader = shader, IsAntialias = false, BlendMode = SKBlendMode.Src };
        canvas.DrawRect(deviceRect, paint);
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
