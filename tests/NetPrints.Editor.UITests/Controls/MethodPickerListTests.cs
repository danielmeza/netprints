using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NetPrints.Core;
using NetPrints.Editor.Controls;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Controls;

/// <summary>The shared method list: a filter box that has focus, keys that move and pick, theme-safe rows, and virtualized rows (FR-094).</summary>
public class MethodPickerListTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static MethodPickerItem Item(Type declaring, string name, int parameters = 0, bool current = false, bool overridden = false, bool isAbstract = false)
    {
        MethodParameter[] arguments = [.. Enumerable.Range(0, parameters).Select(i => new MethodParameter($"p{i}", TypeSpecifier.FromType<int>(), MethodParameterPassType.Default, false, null))];
        var method = new MethodSpecifier(name, arguments, [TypeSpecifier.FromType<string>()], isAbstract ? MethodModifiers.Abstract : MethodModifiers.Virtual,
            MemberVisibility.Public, TypeSpecifier.FromType(declaring), []);
        return MethodPickerItem.For(method, current, overridden);
    }

    private static MethodPickerListViewModel Realistic() => new(
    [
        Item(typeof(Exception), "ToString", current: true),
        Item(typeof(Exception), "GetBaseException", isAbstract: true),
        Item(typeof(object), "Equals", 1),
        Item(typeof(object), "ToString"),
        Item(typeof(object), "Finalize", overridden: true),
    ]);

    private static Window Host(HeadlessUi ui, MethodPickerListViewModel list) =>
        ui.Show(new Window { Width = 640, Height = 480, Content = new MethodPickerList { DataContext = list } });

    private static T Named<T>(Window window, string automationId) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(control => AutomationProperties.GetAutomationId(control) == automationId);

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheFilterBoxHasFocusWhenTheListIsShown()
    {
        using var ui = HeadlessUi.Create();
        Window window = Host(ui, Realistic());

        TextBox filter = Named<TextBox>(window, AutomationIds.MethodPickerFilter);

        Assert.Same(filter, window.FocusManager?.GetFocusedElement());
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TypingFiltersTheRows()
    {
        using var ui = HeadlessUi.Create();
        MethodPickerListViewModel list = Realistic();
        Window window = Host(ui, list);

        await ui.Driver.TypeAsync("tostr", Token);

        Assert.Equal("tostr", list.Filter);
        Assert.Equal(["Exception", "string ToString()", "Object", "string ToString()"], list.Rows.Select(row => row.Text));
        Assert.Equal(4, Named<ListBox>(window, AutomationIds.MethodPickerRows).GetVisualDescendants().OfType<ListBoxItem>().Count());
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DownMovesIntoTheListAndEnterPicksTheSelectedRow()
    {
        using var ui = HeadlessUi.Create();
        MethodPickerListViewModel list = Realistic();
        Window window = Host(ui, list);
        var picked = new List<MethodPickerItem>();
        list.Picked += (_, item) => picked.Add(item);
        await ui.Driver.TypeAsync("tostr", Token);

        await ui.Driver.PressAsync("Down", Token);
        ListBox rows = Named<ListBox>(window, AutomationIds.MethodPickerRows);
        Assert.True(rows.IsKeyboardFocusWithin, $"focus is on {window.FocusManager?.GetFocusedElement()}");

        await ui.Driver.PressAsync("Down", Token);
        Assert.Equal("Object", list.Selected?.Item?.DeclaringTypeName);
        await ui.Driver.PressAsync("Enter", Token);

        MethodPickerItem item = Assert.Single(picked);
        Assert.Equal("Object", item.DeclaringTypeName);
        Assert.Equal("ToString", item.Name);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task EnterInTheFilterBoxPicksTheFirstPickableRow()
    {
        using var ui = HeadlessUi.Create();
        MethodPickerListViewModel list = Realistic();
        Host(ui, list);
        var picked = new List<MethodPickerItem>();
        list.Picked += (_, item) => picked.Add(item);

        await ui.Driver.PressAsync("Enter", Token);

        Assert.Equal("ToString", Assert.Single(picked).Name);
        Assert.Equal("Exception", picked[0].DeclaringTypeName);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task EscapeCancels()
    {
        using var ui = HeadlessUi.Create();
        MethodPickerListViewModel list = Realistic();
        Host(ui, list);
        int cancels = 0;
        list.Cancelled += (_, _) => cancels++;

        await ui.Driver.PressAsync("Esc", Token);

        Assert.Equal(1, cancels);
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void HeadersTheCurrentMarkAndDimmedRowsUseTokensAndClasses(string variant)
    {
        using var ui = HeadlessUi.Create();
        Window window = Host(ui, Realistic());
        window.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        HeadlessDriverPump();

        Grid[] rows = [.. window.GetVisualDescendants().OfType<Grid>().Where(grid => grid.Classes.Contains("methodRow"))];
        Grid header = rows.First(row => row.Classes.Contains("header"));
        Grid current = rows.Single(row => row.Classes.Contains("current"));
        Grid dimmed = rows.Single(row => row.Classes.Contains("dimmed"));

        Assert.Equal(FontWeight.SemiBold, header.GetVisualDescendants().OfType<TextBlock>().First().FontWeight);
        Assert.Equal(FontWeight.SemiBold, current.GetVisualDescendants().OfType<TextBlock>().First().FontWeight);
        Assert.True(dimmed.Opacity < 1);
        Assert.Equal(1, header.Opacity);

        TextBlock mark = current.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Classes.Contains("methodMark") && text.IsEffectivelyVisible);
        Assert.True(window.TryFindResource("SystemControlForegroundBaseMediumBrush", window.ActualThemeVariant, out object? token));
        Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(token).Color,Assert.IsAssignableFrom<ISolidColorBrush>(mark.Foreground).Color);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheFilterBoxAndTheListHaveAutomationIds()
    {
        using var ui = HeadlessUi.Create();
        Window window = Host(ui, Realistic());

        Assert.NotNull(Named<TextBox>(window, AutomationIds.MethodPickerFilter));
        Assert.NotNull(Named<ListBox>(window, AutomationIds.MethodPickerRows));
        Assert.NotNull(Named<MethodPickerList>(window, AutomationIds.MethodPicker));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void OnlyTheVisibleRowsAreRealizedInALongList()
    {
        using var ui = HeadlessUi.Create();
        MethodPickerItem[] items = [.. Enumerable.Range(0, 80).Select(i => Item(typeof(object), $"Method{i:D2}"))];
        Window window = Host(ui, new MethodPickerListViewModel(items));

        int realized = window.GetVisualDescendants().OfType<ListBoxItem>().Count();

        Assert.InRange(realized, 1, 40);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheListKeepsItsHeightWhileFiltering()
    {
        using var ui = HeadlessUi.Create();
        Window window = Host(ui, Realistic());
        ListBox rows = Named<ListBox>(window, AutomationIds.MethodPickerRows);
        double before = rows.Bounds.Height;

        await ui.Driver.TypeAsync("zzz", Token);

        Assert.True(before > 0);
        Assert.Equal(before, rows.Bounds.Height);
        Assert.True(Named<EmptyState>(window, AutomationIds.MethodPickerEmpty).IsEffectivelyVisible);
    }

    private static void HeadlessDriverPump() => Driving.HeadlessDriver.Pump();
}
