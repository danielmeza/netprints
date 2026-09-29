using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.Dialogs;

namespace NetPrints.Editor.Tests.Dialogs;

/// <summary>
/// Batch X2b: <c>SelectCommand</c>/<c>TrustCommand</c>/<c>DontLoadCommand</c> set <see
/// cref="DialogVM{TResult}.Result"/> and raise <see cref="IDialogCloseSource.CloseRequested"/>; the
/// view (<see cref="NetPrints.Editor.Behaviors.DialogCloseBehavior"/>) is exercised separately by the
/// headless dialog tests.
/// </summary>
public class DialogVMTests
{
    private static readonly TypeSpecifier StringType = TypeSpecifier.FromType<string>();

    [Fact]
    public void SelectMethodDialogPreselectsFirstAndClosesWithSelection()
    {
        var trim = new MethodSpecifier("Trim", [], [StringType], MethodModifiers.None, MemberVisibility.Public, StringType, []);
        var toUpper = new MethodSpecifier("ToUpper", [], [StringType], MethodModifiers.None, MemberVisibility.Public, StringType, []);
        var vm = new SelectMethodDialogVM([trim, toUpper]);
        bool closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        Assert.Equal(trim, vm.SelectedMethod); // PAR-59
        Assert.True(vm.SelectCommand.CanExecute(null));

        vm.SelectedMethod = toUpper;
        vm.SelectCommand.Execute(null);

        Assert.True(closed);
        Assert.Equal(toUpper, vm.Result);
    }

    [Fact]
    public void SelectMethodDialogCannotSelectWithNoMethods()
    {
        var vm = new SelectMethodDialogVM([]);

        Assert.Null(vm.SelectedMethod);
        Assert.False(vm.SelectCommand.CanExecute(null));
    }

    [Fact]
    public void SelectTypeDialogResolvesSelectedItemOverTypedText()
    {
        TypeSpecifier[] types = [TypeSpecifier.FromType<object>(), StringType, TypeSpecifier.FromType<int>()];
        var vm = new SelectTypeDialogVM(types, TypeSpecifier.FromType<object>());

        Assert.Equal(TypeSpecifier.FromType<object>(), vm.ResolveSelection()); // PAR-58
        Assert.Equal("System.Object", vm.TypedText);

        // The real AutoCompleteBox clears its own SelectedItem once the user edits the text away from
        // it (DialogTests.SelectTypeDefaultsToObjectAndResolvesText exercises that through the control);
        // this VM test drives the same state directly.
        vm.SelectedType = null;
        vm.TypedText = "System.String";
        Assert.Equal(StringType, vm.ResolveSelection()); // falls back to the typed text

        vm.SelectedType = TypeSpecifier.FromType<int>();
        Assert.Equal(TypeSpecifier.FromType<int>(), vm.ResolveSelection()); // the drop-down choice wins
    }

    [Fact]
    public void SelectTypeDialogCloseSendsResolvedSelection()
    {
        var vm = new SelectTypeDialogVM([StringType], StringType);
        bool closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        vm.SelectCommand.Execute(null);

        Assert.True(closed);
        Assert.Equal(StringType, vm.Result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TrustDialogAnswersWithTheCommandUsed(bool trust)
    {
        var vm = new TrustDialogVM("/work/P.csproj", ["/work/ext-a"]);
        bool closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        Assert.Contains("/work/P.csproj", vm.Prompt);
        Assert.Equal(["/work/ext-a"], vm.ExtensionFolders);

        if (trust)
        {
            vm.TrustCommand.Execute(null);
        }
        else
        {
            vm.DontLoadCommand.Execute(null);
        }

        Assert.True(closed);
        Assert.Equal(trust, vm.Result);
    }

    [Fact]
    public void IssuesDialogVMFormatsEachDiagnosticAsIdColonMessage()
    {
        var first = new CodeDiagnostic(CodeDiagnosticSeverity.Error, "NPD001", "boom", null, null, null, null, null);
        var second = new CodeDiagnostic(CodeDiagnosticSeverity.Warning, "NPD002", "careful", null, null, null, null, null);

        var vm = new IssuesDialogVM([first, second]);

        Assert.Equal(["NPD001: boom", "NPD002: careful"], vm.Issues);
    }

    [Fact]
    public void ErrorDialogVMExposesTheMessageAsGiven()
    {
        var vm = new ErrorDialogVM("details\nline 2");

        Assert.Equal("details\nline 2", vm.Message);
    }
}
