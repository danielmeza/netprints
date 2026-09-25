#nullable enable
using System; using System.Diagnostics; using Avalonia; using Avalonia.Controls; using Avalonia.Media; using Avalonia.Threading; using SkiaSharp;
AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
class App : Application {
  public override void OnFrameworkInitializationCompleted() {
    var grid = new GridBackground{ ViewportZoom = 0.47, ViewportLocation = new Point(-123.4, 56.7), Mode = GridRenderMode.Shader };
    var w = new Window { Width = 1600, Height = 900, Background = new SolidColorBrush(Color.FromRgb(0x20,0x20,0x20)), Content = grid };
    var hashes = new System.Collections.Generic.Dictionary<string,string>(); var sw = new Stopwatch(); int frames = 0; double drawMs = 0;
    GridBackground.AfterDraw = (lease, rect, shader, gpu) => {
      frames++;
      if (frames == 3 || frames == 8) {
        using var img = lease.SkSurface!.Snapshot(SKRectI.Round(rect)); using var raster = img.ToRasterImage(); using var pm = raster.PeekPixels();
        var span = pm.GetPixelSpan(); var h = System.Security.Cryptography.SHA1.HashData(span); hashes[shader?"shader":"cpu"] = Convert.ToHexString(h)[..12];
        Console.WriteLine($"frame {frames}: shader={shader} gpu={gpu} backend={lease.GrContext?.Backend} hash={hashes[shader?"shader":"cpu"]} size={pm.Width}x{pm.Height}");
      }
    };
    int tick = 0;
    var t = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Normal, (_, _) => {
      tick++;
      if (tick == 5) grid.Mode = GridRenderMode.Cpu;          // frame 3 = shader, later frames = cpu
      else if (tick < 12) grid.ViewportZoom = tick >= 5 ? 0.47 : 0.47 + 0; 
      if (tick == 5) { } 
      if (tick == 12) { Console.WriteLine(hashes.Count == 2 && hashes["shader"] == hashes["cpu"] ? "GPU shader == CPU path (pixel identical)" : "GPU shader differs from CPU path"); w.Close(); }
      grid.InvalidateVisual();
    });
    w.Show(); t.Start();
    base.OnFrameworkInitializationCompleted();
  }
}
