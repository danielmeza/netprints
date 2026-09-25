using Avalonia.Controls;
using Avalonia.Headless;
using NetPrints.Core;
using NetPrints.Editor.Tests.Fakes;
using NetPrints.Editor.ViewModels;
using NetPrints.Editor.Views;
using NetPrints.Editor.Views.Dialogs;

namespace NetPrints.Editor.Tests.Ui;

[TestClass]
public class DialogTests
{
    [TestMethod]
    [Timeout(60000, CooperativeCancellation = true)]
    public Task SelectTypeDefaultsToObjectAndResolvesText() => UiTest.RunAsync(() =>
    {
        var types = TestEditor.SharedReflectionHost.NonStaticTypes;
        var dialog = new SelectTypeDialog(types, TypeSpecifier.FromType<object>());
        dialog.Show();
        UiTest.Pump();

        Assert.AreEqual(TypeSpecifier.FromType<object>(), dialog.ResolveSelection(), "defaults to object (PAR-58)");

        var box = dialog.Find<AutoCompleteBox>("TypeBox");
        box.SelectedItem = null;
        box.Text = "System.String";
        Assert.AreEqual(TypeSpecifier.FromType<string>(), dialog.ResolveSelection(), "the chooser is editable");
        dialog.Close();
    });

    [TestMethod]
    [Timeout(60000, CooperativeCancellation = true)]
    public Task SelectMethodPreselectsFirst() => UiTest.RunAsync(() =>
    {
        var methods = TestEditor.SharedReflectionHost.Provider
            .GetMethods(new NetPrints.Reflection.ReflectionProviderMethodQuery().WithType(TypeSpecifier.FromType<string>()).WithStatic(false))
            .Take(5).ToList();
        var dialog = new SelectMethodDialog(methods);
        dialog.Show();
        UiTest.Pump();

        Assert.AreEqual(methods[0], dialog.SelectedMethod, "PAR-59");
        Assert.IsNotNull(dialog.CaptureRenderedFrame());
        dialog.Close();
    });

    [TestMethod]
    [Timeout(60000, CooperativeCancellation = true)]
    public Task ErrorDialogShowsCopyableMessage() => UiTest.RunAsync(() =>
    {
        var dialog = new ErrorDialog("Failed", "details\nline 2");
        dialog.Show();
        UiTest.Pump();

        Assert.AreEqual("Failed", dialog.Title);
        var box = dialog.Find<TextBox>("MessageBox");
        Assert.IsTrue(box.IsReadOnly);
        Assert.AreEqual("details\nline 2", box.Text);
        dialog.Find<Button>("OkButton").Command?.Execute(null);
        dialog.Close();
    });

    [TestMethod]
    [Timeout(60000, CooperativeCancellation = true)]
    public Task ReferencesDialogListsAndCloses() => UiTest.RunAsync(() =>
    {
        var project = Project.CreateNew("P", "N");
        project.References.Add(new SourceDirectoryReference("/tmp/src"));
        var dialog = new ReferencesDialog { DataContext = new ReferenceListVM(project, new TestEditor().Context) };
        dialog.Show();
        UiTest.Pump();

        var switches = dialog.Descendants<ToggleSwitch>().ToList();
        Assert.HasCount(4, switches, "one row per reference (PAR-16)");
        Assert.HasCount(1, switches.Where(s => s.IsEffectivelyEnabled).ToList(), "Include/Exclude only for sources (PAR-19)");
        Assert.IsTrue(dialog.Descendants<TextBlock>().Any(t => t.Text?.Contains("System.dll") == true));

        bool closed = false;
        dialog.Closed += (_, _) => closed = true;
        dialog.Click(dialog.Find<Button>("CloseButton").CenterIn(dialog));
        Assert.IsTrue(closed, "PAR-21");
    });
}
