#nullable enable
using System; using Avalonia; using Avalonia.Controls; using Avalonia.Headless; using Avalonia.Media; using Avalonia.Threading;
var session = HeadlessUnitTestSession.StartNew(typeof(App));
await session.Dispatch(() => {
  Avalonia.Media.Imaging.WriteableBitmap? Snap(GridRenderMode mode) { return null; }
  var results = new System.Collections.Generic.List<Avalonia.Media.Imaging.Bitmap>();
  foreach (var mode in new[]{GridRenderMode.Auto, GridRenderMode.Shader, GridRenderMode.Cpu}) {
    var grid = new GridBackground{ ViewportZoom = 0.47, ViewportLocation = new Point(-123.4, 56.7), Mode = mode };
    var w = new Window { Width = 320, Height = 200, Background = new SolidColorBrush(Color.FromRgb(0x20,0x20,0x20)), Content = new Border{ Margin = new Thickness(7.3, 3.1), Child = grid } };
    w.Show(); Dispatcher.UIThread.RunJobs();
    var f = w.CaptureRenderedFrame()!; results.Add(f);
    Console.WriteLine($"{mode}: path={GridBackground.LastPath}");
    w.Close();
  }
  using var ms1 = new System.IO.MemoryStream(); results[1].Save(ms1); using var ms2 = new System.IO.MemoryStream(); results[2].Save(ms2);
  using var a = SkiaSharp.SKBitmap.Decode(ms1.ToArray()); using var b = SkiaSharp.SKBitmap.Decode(ms2.ToArray());
  int diff=0,maxD=0; for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++){var p=a.GetPixel(x,y);var q=b.GetPixel(x,y);int d=Math.Max(Math.Abs(p.Red-q.Red),Math.Max(Math.Abs(p.Green-q.Green),Math.Abs(p.Blue-q.Blue)));if(d>0)diff++;maxD=Math.Max(maxD,d);}
  Console.WriteLine($"headless shader vs cpu: differing px={diff}/{a.Width*a.Height}, max channel diff={maxD}");
  System.IO.File.WriteAllBytes("../headless-cpu.png", ms2.ToArray());
  return 0;
}, default);
class App : Application { public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions{UseHeadlessDrawing=false}); }
