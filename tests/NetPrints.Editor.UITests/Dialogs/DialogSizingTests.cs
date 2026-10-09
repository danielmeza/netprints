using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.References;
using NetPrints.Editor.StartPage;
using NetPrints.Editor.State;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Projects;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Workspace;

namespace NetPrints.Editor.UITests.Dialogs;

/// <summary>The older dialogs size to their content: their buttons stay inside the window and their width stays between the dialog tokens (FR-088).</summary>
public class DialogSizingTests
{
    private static double Token(string key) =>
        Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out object? value) && value is double number ? number : double.NaN;

    private static void AssertSizedToContent(Window window)
    {
        Assert.NotEqual(SizeToContent.Manual, window.SizeToContent);
        Assert.InRange(window.ClientSize.Width, Token("Dialog.MinWidth"), Token("Dialog.MaxWidth"));
        Button[] buttons = [.. window.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible)];
        Assert.NotEmpty(buttons);
        foreach (Button button in buttons)
        {
            Point origin = button.TranslatePoint(default, window) ?? new Point(double.NaN, double.NaN);
            Assert.True(
                origin.X >= 0 && origin.Y >= 0 && origin.X + button.Bounds.Width <= window.ClientSize.Width && origin.Y + button.Bounds.Height <= window.ClientSize.Height,
                $"{button.Content} at {origin} ({button.Bounds.Width}x{button.Bounds.Height}) lies outside {window.ClientSize}");
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheSampleTargetDialogFitsItsContent()
    {
        using var ui = HeadlessUi.Create();

        Window window = ui.Show(new SampleTargetDialog("HelloWorld", "/projects/some/deeply/nested/folder/that/is/long/enough/to/wrap/HelloWorld"));

        AssertSizedToContent(window);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheSelectTypeDialogFitsItsContent()
    {
        using var ui = HeadlessUi.Create();
        TypeSpecifier[] types = [TypeSpecifier.FromType<object>(), TypeSpecifier.FromType<string>(), TypeSpecifier.FromType<int>()];

        Window window = ui.Show(new SelectTypeDialog(types, TypeSpecifier.FromType<object>()));

        AssertSizedToContent(window);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheNewProjectDialogFitsItsContent()
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        var projects = new MsBuildProjectSystem(new ProjectSystemOptions([], "1.0.0"), new ProcessRunner(), NullLogger<MsBuildProjectSystem>.Instance);
        var service = new ProjectTemplateService(() => registry.ProjectTemplates, _ => DefaultProjectProfile.Instance, projects);
        var viewModel = new NewProjectDialogViewModel(service, new QueuedFilePicker(), new ProjectLocations(null, "/projects"), TimeProvider.System);
        using var ui = HeadlessUi.Create();

        Window window = ui.Show(new NewProjectDialog(viewModel));

        AssertSizedToContent(window);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheReferencesDialogFitsItsContent()
    {
        using var rig = new ReferencesRig();
        using var ui = HeadlessUi.Create();

        Window window = ui.Show(new ReferencesDialog { DataContext = rig.References });

        AssertSizedToContent(window);
    }
}
