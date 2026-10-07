using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Xaml.Interactivity;
using NetPrints.Editor.Behaviors;
using NetPrints.Editor.State;
using NetPrints.Editor.UITests.Hosting;
using AvaloniaWindowState = Avalonia.Controls.WindowState;

namespace NetPrints.Editor.UITests.State;

/// <summary>The window's behavior restores the saved bounds and maximized state, and saves them when the window closes.</summary>
public class WindowStateBehaviorTests
{
    private sealed class FakeStore : IEditorStateStore
    {
        public NetPrints.Editor.State.WindowState? Window { get; set; }

        public NetPrints.Editor.State.WindowState? LoadWindow() => Window;

        public void SaveWindow(NetPrints.Editor.State.WindowState state) => Window = state;

        public LayoutState? LoadLayout() => null;

        public void SaveLayout(LayoutState state)
        {
        }

        public RecentState LoadRecent() => RecentState.Empty;

        public void SaveRecent(RecentState state)
        {
        }

        public SessionState? LoadSession(string projectPath) => null;

        public void SaveSession(SessionState state)
        {
        }

        public StartState? LoadStart() => null;

        public void SaveStart(StartState state)
        {
        }
    }

    private static Window CreateWindow(FakeStore store)
    {
        var window = new Window { Width = 400, Height = 300 };
        Interaction.GetBehaviors(window).Add(new WindowStateBehavior { Service = new WindowStateService(store) });
        return window;
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheSavedBoundsAreRestoredWhenTheWindowOpens()
    {
        var store = new FakeStore { Window = new NetPrints.Editor.State.WindowState(StateFile.CurrentVersion, 30, 40, 700, 500, false) };
        Window window = CreateWindow(store);

        window.Show();

        Assert.Equal(new PixelPoint(30, 40), window.Position);
        Assert.Equal(700, window.Width * window.RenderScaling, 1);
        Assert.Equal(500, window.Height * window.RenderScaling, 1);
        Assert.Equal(AvaloniaWindowState.Normal, window.WindowState);
        window.Close();
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheMaximizedStateIsRestored()
    {
        var store = new FakeStore { Window = new NetPrints.Editor.State.WindowState(StateFile.CurrentVersion, 30, 40, 700, 500, true) };
        Window window = CreateWindow(store);

        window.Show();

        Assert.Equal(AvaloniaWindowState.Maximized, window.WindowState);
        window.Close();
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ClosingTheWindowSavesItsNormalBoundsAndState()
    {
        var store = new FakeStore { Window = new NetPrints.Editor.State.WindowState(StateFile.CurrentVersion, 30, 40, 700, 500, false) };
        Window window = CreateWindow(store);
        window.Show();
        window.Position = new PixelPoint(55, 65);
        window.WindowState = AvaloniaWindowState.Maximized;

        window.Close();

        NetPrints.Editor.State.WindowState? saved = store.Window;
        Assert.NotNull(saved);
        Assert.True(saved.IsMaximized);
        Assert.Equal((55, 65), (saved.X, saved.Y));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void WithoutSavedStateTheWindowKeepsItsDefaults()
    {
        var store = new FakeStore();
        Window window = CreateWindow(store);

        window.Show();

        Assert.Equal(400, window.Width);
        Assert.Equal(AvaloniaWindowState.Normal, window.WindowState);
        window.Close();
    }
}
