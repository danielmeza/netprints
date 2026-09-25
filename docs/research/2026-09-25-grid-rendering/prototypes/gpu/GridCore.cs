#nullable enable
using System;
using SkiaSharp;

/// Single source of truth for the grid's look; both render paths read only this.
public sealed record GridStyle(
    double CellSize = 28, int MajorEvery = 5,
    double MinorWidthDip = 1, double MajorWidthDip = 1,
    SKColor MinorColor = default, SKColor MajorColor = default,
    double MinorFadeOutDip = 6, double MinorFadeInDip = 14);   // minor cell size (DIP) where minor lines vanish / reach full alpha

/// Everything a frame needs, in device pixels. Computed once (CPU, double precision), consumed by both paths.
public readonly record struct GridFrame(
    float PhaseX, float PhaseY, float Cell, float MajorEvery,
    float MinorW, float MajorW, SKColor Minor, SKColor Major)
{
    public static GridFrame Compute(GridStyle s, double locX, double locY, double zoom, double scaling,
                                    double deviceOriginX, double deviceOriginY, double opacity)
    {
        double cellDip = s.CellSize * zoom;
        double cellDev = cellDip * scaling;
        double period = cellDev * s.MajorEvery;
        // device x of graph x=0, reduced modulo one major period so floats stay small and the major index is preserved
        double px = Mod(deviceOriginX - locX * zoom * scaling, period);
        double py = Mod(deviceOriginY - locY * zoom * scaling, period);
        double t = Math.Clamp((cellDip - s.MinorFadeOutDip) / (s.MinorFadeInDip - s.MinorFadeOutDip), 0, 1);
        double minorFade = t * t * (3 - 2 * t);                    // smoothstep: fade, never pop
        return new GridFrame((float)px, (float)py, (float)cellDev, s.MajorEvery,
            (float)Math.Max(1, Math.Round(s.MinorWidthDip * scaling)),
            (float)Math.Max(1, Math.Round(s.MajorWidthDip * scaling)),
            s.MinorColor.WithAlpha((byte)Math.Round(s.MinorColor.Alpha * minorFade * opacity)),
            s.MajorColor.WithAlpha((byte)Math.Round(s.MajorColor.Alpha * opacity)));
    }
    static double Mod(double a, double m) => a - m * Math.Floor(a / m);
}

public static class GridRenderer
{
    // Pixel-snapped, non-AA procedural grid. Same integer column/row rule as DrawCpu, so both paths match pixel for pixel.
    public const string Sksl = @"
uniform float2 phase; uniform float cell; uniform float majorEvery;
uniform float minorW; uniform float majorW;
uniform half4 minorColor; uniform half4 majorColor;   // premultiplied

float2 hits(float c, float o) {                          // x = minor coverage, y = major coverage (0 or 1)
    float col = floor(c);
    float k = floor((c - o) / cell + 0.5);               // nearest line index
    float L = o + k * cell;
    float m = k - majorEvery * floor(k / majorEvery);    // non-negative mod
    bool isMajor = m < 0.5 || m > majorEvery - 0.5;
    float w = isMajor ? majorW : minorW;
    float col0 = floor(L + 0.5 - 0.5 * w);
    float hit = (col >= col0 && col < col0 + w) ? 1.0 : 0.0;
    return isMajor ? float2(0.0, hit) : float2(hit, 0.0);
}

half4 main(float2 p) {                                    // p = device pixel centre (canvas matrix is reset)
    float2 hx = hits(p.x, phase.x);
    float2 hy = hits(p.y, phase.y);
    half minor = half(max(hx.x, hy.x));
    half major = half(max(hx.y, hy.y));
    half4 mi = minorColor * minor;
    half4 ma = majorColor * major;
    return ma + mi * (1.0 - ma.a);                        // major drawn over minor, like DrawCpu
}";

    static readonly Lazy<SKRuntimeEffect?> effect = new(() => SKRuntimeEffect.CreateShader(Sksl, out _));
    public static bool ShaderAvailable => effect.Value is not null;

    public static void DrawShader(SKCanvas c, SKRect deviceRect, in GridFrame f)
    {
        var e = effect.Value!;
        using var u = new SKRuntimeEffectUniforms(e)
        {
            ["phase"] = new SKPoint(f.PhaseX, f.PhaseY), ["cell"] = f.Cell, ["majorEvery"] = f.MajorEvery,
            ["minorW"] = f.MinorW, ["majorW"] = f.MajorW,
            ["minorColor"] = Premul(f.Minor), ["majorColor"] = Premul(f.Major),
        };
        using var shader = e.ToShader(u);
        using var paint = new SKPaint { Shader = shader, IsAntialias = false };
        c.DrawRect(deviceRect, paint);
    }

    [ThreadStatic] static SKPath? minorPath, majorPath;
    [ThreadStatic] static SKPaint? paint;

    public static void DrawCpu(SKCanvas c, SKRect r, in GridFrame f)
    {
        var minor = minorPath ??= new SKPath(); var major = majorPath ??= new SKPath();
        minor.Rewind(); major.Rewind();
        AddAxis(minor, major, f, r.Left, r.Right, f.PhaseX, vertical: true, r);
        AddAxis(minor, major, f, r.Top, r.Bottom, f.PhaseY, vertical: false, r);
        var p = paint ??= new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
        if (f.Minor.Alpha > 0) { p.Color = f.Minor; c.DrawPath(minor, p); }   // nonzero fill = union: crossings blend once
        if (f.Major.Alpha > 0) { p.Color = f.Major; c.DrawPath(major, p); }
    }

    static void AddAxis(SKPath minor, SKPath major, in GridFrame f, float from, float to, float o, bool vertical, SKRect r)
    {
        int kStart = (int)MathF.Floor((from - o) / f.Cell) - 1, kEnd = (int)MathF.Ceiling((to - o) / f.Cell) + 1;
        for (int k = kStart; k <= kEnd; k++)
        {
            bool isMajor = ((k % (int)f.MajorEvery) + (int)f.MajorEvery) % (int)f.MajorEvery == 0;
            if (!isMajor && f.Minor.Alpha == 0) continue;                            // LOD: skip hidden minors entirely
            float w = isMajor ? f.MajorW : f.MinorW;
            float col0 = MathF.Floor(o + k * f.Cell + 0.5f - 0.5f * w);
            var rect = vertical ? new SKRect(col0, r.Top, col0 + w, r.Bottom) : new SKRect(r.Left, col0, r.Right, col0 + w);
            (isMajor ? major : minor).AddRect(rect);
        }
    }

    static SKColorF Premul(SKColor c) { float a = c.Alpha / 255f; return new SKColorF(c.Red / 255f * a, c.Green / 255f * a, c.Blue / 255f * a, a); }
}
