using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.Commands.CommandPalette;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Commands;
using DocumentId = NetPrints.Editor.Shell.DocumentId;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Extensibility.Settings;
using NetPrints.Projects;
using ShellWindow = NetPrints.Editor.Shell.ShellWindow;

namespace NetPrints.Editor.UITests.Theming;

/// <summary>Focus ring, hover and pressed states and the density tokens on the themed controls (FR-087).</summary>
public class InteractionStateTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class Probe : ICommandHandler
    {
        public bool CanExecute(CommandContext context) => true;

        public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class Contexts : ICommandContextProvider
    {
        public event EventHandler? CommandStatesChanged
        {
            add { }
            remove { }
        }

        public CommandContext Create(object? parameter = null, CommandScope scope = CommandScope.Global) =>
            new(new KeyStubShell(), null, null, null, CommandSelection.None, parameter, scope);
    }

    private static object? Resolve(string key, ThemeVariant variant) =>
        Application.Current is { } app && app.TryGetResource(key, variant, out object? value) ? value : null;

    private static Color ColourOf(string key, ThemeVariant variant) =>
        Assert.IsAssignableFrom<ISolidColorBrush>(Resolve(key, variant)).Color;

    public static TheoryData<string> Kinds() => ["tree item", "document tab", "command-bar button", "menu item", "errors row"];

    public static TheoryData<string> TransitionKinds() => ["tree item", "document tab", "command-bar button", "errors row"];

    private static async Task<(EditorSession Session, Control Control)> OpenAsync(string kind)
    {
        EditorSession session = await EditorSession.OpenSampleMainAsync(Token);
        if (kind == "errors row")
        {
            var project = session.App.Session.Project;
            ClassGraph cls = project.Classes.Single();
            project.LastDiagnostics = new ObservableRangeCollection<CodeDiagnostic>(
                [new CodeDiagnostic(CodeDiagnosticSeverity.Error, "CS1503", "boom", cls.FullName, GraphKeys.For(cls.Methods.First()), cls.Methods.First().Nodes.First().Id, null, null)]);
            HeadlessDriver.Pump();
        }

        var window = session.Window;
        Control control = kind switch
        {
            "tree item" => window.GetVisualDescendants().OfType<TreeViewItem>().First(),
            "document tab" => await UnselectedTabAsync(session),
            "command-bar button" => window.GetVisualDescendants().OfType<Button>().First(button => button.Classes.Contains("command-bar")),
            "menu item" => window.GetVisualDescendants().OfType<MenuItem>().First(),
            "errors row" => window.GetVisualDescendants().OfType<ListBoxItem>().First(item => AutomationProperties.GetAutomationId(item) == AutomationIds.ErrorsRow),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
        return (session, control);
    }

    private static Task<Control> UnselectedTabAsync(EditorSession session)
    {
        ClassGraph cls = session.Class;
        session.App.Api.OpenDocument(DocumentId.Graph(session.App.Session.ClassPathOf(cls), DocumentId.ClassGraphKey));
        HeadlessDriver.Pump();
        Control tab = session.Window.GetVisualDescendants().OfType<DocumentTabStripItem>().First(item => !item.IsSelected);
        return Task.FromResult(tab);
    }

    private static Border RingOf(Control control)
    {
        if (control.IsFocused && TopLevel.GetTopLevel(control) is { } top)
        {
            top.GetVisualDescendants().OfType<InputElement>().First(other => other.Focusable && other.IsEffectivelyVisible && !ReferenceEquals(other, control)).Focus();
            HeadlessDriver.Pump();
        }

        control.Focus(NavigationMethod.Tab);
        HeadlessDriver.Pump();
        Control adorner = AdornerLayer.GetAdornerLayer(control)?.Children.OfType<Control>().FirstOrDefault(child => AdornerLayer.GetAdornedElement(child) is { } adorned && (ReferenceEquals(adorned, control) || control.IsVisualAncestorOf(adorned)))
            ?? throw new InvalidOperationException($"{control.GetType().Name} has no focus adorner.");
        return adorner as Border ?? adorner.GetVisualDescendants().OfType<Border>().First();
    }

    private static void AssertRing(Border ring, ThemeVariant variant)
    {
        Assert.Equal(Assert.IsType<Thickness>(Resolve("Focus.RingThickness", variant)), ring.BorderThickness);
        Assert.Equal(ColourOf("Focus.Ring", variant), Assert.IsAssignableFrom<ISolidColorBrush>(ring.BorderBrush).Color);
        Assert.Equal(Assert.IsType<CornerRadius>(Resolve("Radius.Control", variant)), ring.CornerRadius);
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(Kinds))]
    public async Task KeyboardFocusShowsTheRingWithTheControlsCornerRadius(string kind)
    {
        var (session, control) = await OpenAsync(kind);
        await using (session)
        {
            Assert.Equal(new Thickness(2), Resolve("Focus.RingThickness", ThemeVariant.Dark));
            AssertRing(RingOf(control), ThemeVariant.Dark);
            if (control is TemplatedControl { CornerRadius: var radius })
            {
                Assert.Equal(Assert.IsType<CornerRadius>(Resolve("Radius.Control", ThemeVariant.Dark)), radius);
            }
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheFocusRingFollowsTheVariant()
    {
        Assert.NotEqual(ColourOf("Focus.Ring", ThemeVariant.Dark), ColourOf("Focus.Ring", ThemeVariant.Light));
        Assert.Equal(new Thickness(2), Resolve("Focus.RingThickness", ThemeVariant.Light));
        Assert.Equal(new CornerRadius(4), Resolve("Radius.Control", ThemeVariant.Light));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheCommandPaletteTextBoxShowsTheRing()
    {
        using var ui = HeadlessUi.Create();
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        registry.AddCommand(new CommandDescriptor(ContributionIds.CommandPrefix + "save", "Save", new Probe()));
        registry.Freeze();
        var palette = new CommandPaletteViewModel(registry, new CommandInvoker(registry, new Contexts(), exception => throw exception));
        var dialog = ui.Show(new CommandPaletteDialog(palette));

        TextBox box = dialog.GetVisualDescendants().OfType<TextBox>().First();

        AssertRing(RingOf(box), ThemeVariant.Dark);
        Assert.Equal(new CornerRadius(4), box.CornerRadius);
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(Kinds))]
    public async Task PointerOverAndPressedUseTheStateTokensInBothVariants(string kind)
    {
        var (session, control) = await OpenAsync(kind);
        await using (session)
        {
            // Hover first in both variants: pressing selects the row, and a selected row keeps its selection colour under the pointer.
            foreach (bool pressed in new[] { false, true })
            {
                foreach (ThemeVariant variant in new[] { ThemeVariant.Dark, ThemeVariant.Light })
                {
                    Application.Current!.RequestedThemeVariant = variant;
                    try
                    {
                        if (control is DocumentTabStripItem { IsSelected: true })
                        {
                            session.Window.GetVisualDescendants().OfType<DocumentTabStripItem>().First(item => !ReferenceEquals(item, control)).IsSelected = true;
                            HeadlessDriver.Pump();
                            session.Window.UpdateLayout();
                        }

                        HeadlessDriver.Pump();
                        var window = session.Window;
                        double apart = variant == ThemeVariant.Light ? 12 : 0;
                        Point inside = control.TranslatePoint(new Point(control.Bounds.Width / 2 + apart,Math.Min(control.Bounds.Height / 2, 10)), window)
                            ?? throw new InvalidOperationException("The control is not in the window.");
                        window.MouseMove(inside);
                        HeadlessDriver.Pump();
                        if (pressed)
                        {
                            window.MouseDown(inside, MouseButton.Left);
                            HeadlessDriver.Pump();
                        }

                        string token = pressed ? "State.Pressed" : "State.Hover";
                        Assert.True(PaintsWith(control, ColourOf(token, variant)), $"{kind} {token} in {variant} (pointer over: {control.IsPointerOver})");
                        if (pressed)
                        {
                            window.MouseUp(inside, MouseButton.Left);
                        }

                        window.MouseMove(new Point(window.Bounds.Width - 1, window.Bounds.Height - 1));
                        HeadlessDriver.Pump();
                    }
                    finally
                    {
                        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
                    }
                }
            }
        }
    }

    private sealed class AnimationSettings(bool enabled) : ISettingsStore
    {
        public T Get<T>(ExtensionSettingsDescriptor<T> descriptor) =>
            (object)new NetPrintsSettings { EnableAnimations = enabled } is T value ? value : descriptor.Default;

        public ValueTask SetAsync<T>(ExtensionSettingsDescriptor<T> descriptor, T value, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private static EditorApp App => Assert.IsType<EditorApp>(Application.Current);

    private static double RowHeight => Assert.IsType<double>(Resolve("Density.RowHeight", ThemeVariant.Default));

    private static ListBoxItem RowOf(Control content) =>
        content.FindAncestorOfType<ListBoxItem>() ?? throw new InvalidOperationException("The element is not in a list row.");

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TreeErrorsAndOutputRowsHaveTheDensityHeight()
    {
        var (session, tree) = await OpenAsync("tree item");
        await using (session)
        {
            var project = session.App.Session.Project;
            ClassGraph cls = project.Classes.Single();
            project.LastDiagnostics = new ObservableRangeCollection<CodeDiagnostic>(
                [new CodeDiagnostic(CodeDiagnosticSeverity.Error, "CS1503", "boom", cls.FullName, GraphKeys.For(cls.Methods.First()), cls.Methods.First().Nodes.First().Id, null, null)]);
            HeadlessDriver.Pump();
            ListBoxItem errorRow = session.Window.GetVisualDescendants().OfType<ListBoxItem>().First(item => AutomationProperties.GetAutomationId(item) == AutomationIds.ErrorsRow);

            Assert.Equal(RowHeight, tree.MinHeight);
            Assert.Equal(RowHeight, errorRow.MinHeight);
            Assert.Equal(Assert.IsType<Thickness>(Resolve("Density.RowPadding", ThemeVariant.Default)), errorRow.Padding);

            session.App.Composition.Context.RunState.BuildStarted();
            session.App.Api.ShowPanel(PanelContributions.OutputId);
            HeadlessDriver.Pump();
            ListBoxItem outputRow = RowOf(session.Window.GetVisualDescendants().OfType<TextBlock>().First(text => AutomationProperties.GetAutomationId(text) == AutomationIds.OutputLine));

            Assert.Equal(RowHeight, outputRow.MinHeight);
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ThePaletteRowsHaveTheDensityHeight()
    {
        using var ui = HeadlessUi.Create();
        var dialog = ui.Show(new CommandPaletteDialog(NewPalette()));

        ListBoxItem row = dialog.GetVisualDescendants().OfType<ListBoxItem>().First();

        Assert.Equal(RowHeight, row.MinHeight);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheCommandBarHasTheDensityHeight()
    {
        var (session, _) = await OpenAsync("command-bar button");
        await using (session)
        {
            Border bar = session.Window.GetVisualDescendants().OfType<Border>().First(border => AutomationProperties.GetAutomationId(border) == AutomationIds.ShellCommandBar);

            Assert.Equal(Assert.IsType<double>(Resolve("Density.CommandBarHeight", ThemeVariant.Default)), bar.Height);
        }
    }

    private static CommandPaletteViewModel NewPalette()
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        registry.AddCommand(new CommandDescriptor(ContributionIds.CommandPrefix + "save", "Save", new Probe()));
        registry.Freeze();
        return new CommandPaletteViewModel(registry, new CommandInvoker(registry, new Contexts(), exception => throw exception));
    }

    private static Animatable PartOf(Control control) =>
        control.GetVisualDescendants().OfType<Animatable>().First(part => part is Control { Name: "PART_ContentPresenter" or "PART_LayoutRoot" or "PART_TabBody" });

    private static void AssertFastBrushTransition(Animatable part, string what)
    {
        BrushTransition transition = Assert.Single((part.Transitions ?? []).OfType<BrushTransition>(), t => t.Property?.Name == "Background");
        Assert.Equal(Assert.IsType<TimeSpan>(Resolve("Motion.Fast", ThemeVariant.Default)), transition.Duration);
        Assert.IsType<CubicEaseOut>(transition.Easing);
        Assert.True(transition.Duration == TimeSpan.FromMilliseconds(100), what);
    }

    private static void AssertFade(Animatable root)
    {
        DoubleTransition transition = Assert.Single((root.Transitions ?? []).OfType<DoubleTransition>(), t => t.Property?.Name == "Opacity");
        Assert.Equal(Assert.IsType<TimeSpan>(Resolve("Motion.Normal", ThemeVariant.Default)), transition.Duration);
        Assert.IsType<CubicEaseOut>(transition.Easing);
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(TransitionKinds))]
    public async Task ButtonsTreeAndListRowsAndTabsTransitionTheirBackgroundOverMotionFast(string kind)
    {
        App.EnableTransitions();
        try
        {
            var (session, control) = await OpenAsync(kind);
            await using (session)
            {
                AssertFastBrushTransition(PartOf(control), kind);
            }
        }
        finally
        {
            App.DisableTransitions();
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ThePaletteFadesInOverMotionNormal()
    {
        using var ui = HeadlessUi.Create();
        App.EnableTransitions();
        try
        {
            var dialog = ui.Show(new CommandPaletteDialog(NewPalette()));
            Control root = dialog.GetVisualDescendants().OfType<Control>().First(control => control.Classes.Contains("popupFade"));
            AssertFade(root);
        }
        finally
        {
            App.DisableTransitions();
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheNodeSearchPopupFadesInOverMotionNormal()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        App.EnableTransitions();
        try
        {
            await (await session.Graph.RightClickEmptyAsync(Token)).WaitOpenAsync(Token);
            Control root = session.Window.GetVisualDescendants().OfType<Control>().First(control => control.Classes.Contains("popupFade"));
            AssertFade(root);
        }
        finally
        {
            App.DisableTransitions();
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheAnimationsSettingSwitchesEveryTransitionOffAndOn()
    {
        App.ApplyAnimationSetting(new AnimationSettings(true));
        try
        {
            var (session, button) = await OpenAsync("command-bar button");
            await using (session)
            {
                Control[] themed = [button, session.Window.GetVisualDescendants().OfType<TreeViewItem>().First()];
                Assert.All(themed, control => Assert.NotEmpty(PartOf(control).Transitions ?? []));

                App.ApplyAnimationSetting(new AnimationSettings(false));
                HeadlessDriver.Pump();
                Assert.All(themed, control => Assert.Empty(PartOf(control).Transitions ?? []));
                Assert.All(themed, control => Assert.Empty(control.Transitions ?? []));

                App.ApplyAnimationSetting(new AnimationSettings(true));
                using var ui = HeadlessUi.Create();
                var probe = new Button();
                ui.Show(new Window { Content = probe });
                Assert.NotEmpty(PartOf(probe).Transitions ?? []);
            }
        }
        finally
        {
            App.DisableTransitions();
        }
    }

    private static bool PaintsWith(Control control, Color colour) =>
        control.GetVisualDescendants().Prepend(control).Any(visual =>
            (visual as Border)?.Background is ISolidColorBrush border && border.Color == colour
            || (visual as ContentPresenter)?.Background is ISolidColorBrush presenter && presenter.Color == colour
            || (visual as Panel)?.Background is ISolidColorBrush panel && panel.Color == colour);
}
