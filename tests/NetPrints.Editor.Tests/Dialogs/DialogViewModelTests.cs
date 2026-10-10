using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.Dialogs;

namespace NetPrints.Editor.Tests.Dialogs;

/// <summary>
/// Batch X2b: <c>SelectCommand</c>/<c>TrustCommand</c>/<c>DontLoadCommand</c> set <see
/// cref="DialogViewModel{TResult}.Result"/> and raise <see cref="IDialogCloseSource.CloseRequested"/>; the
/// view (<see cref="NetPrints.Editor.Behaviors.DialogCloseBehavior"/>) is exercised separately by the
/// headless dialog tests.
/// </summary>
public class DialogViewModelTests
{
    private static readonly TypeSpecifier StringType = TypeSpecifier.FromType<string>();

    private static MethodSpecifier Method(Type declaring, string name, int parameters = 0) =>
        new(name, [.. Enumerable.Range(0, parameters).Select(i => new MethodParameter($"p{i}", TypeSpecifier.FromType<int>(), MethodParameterPassType.Default, false, null))],
            [StringType], MethodModifiers.Virtual, MemberVisibility.Public, TypeSpecifier.FromType(declaring), []);

    private static SelectMethodDialogViewModel ExceptionDialog(params string[] overriddenNames) => new(
    [
        Method(typeof(object), "Equals", 1),
        Method(typeof(Exception), "ToString"),
        Method(typeof(object), "ToString"),
        Method(typeof(Exception), "GetBaseException"),
        Method(typeof(object), "Finalize"),
    ], new HashSet<string>(overriddenNames, StringComparer.Ordinal));

    [Fact]
    public void SelectMethodDialogGroupsByDeclaringTypeNearestFirstWithObjectLastAndDropsHiddenBaseMethods()
    {
        SelectMethodDialogViewModel vm = ExceptionDialog();

        Assert.Equal(
        [
            "Exception", "string GetBaseException()", "string ToString()",
            "Object", "string Equals(int p0)", "string Finalize()",
        ], vm.List.Rows.Select(row => row.Text));
    }

    [Fact]
    public void SelectMethodDialogPicksTheFilteredMethodAndClosesWithIt()
    {
        SelectMethodDialogViewModel vm = ExceptionDialog();
        bool closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        vm.List.Filter = "tostr";
        vm.List.PickCommand.Execute(null);

        Assert.True(closed);
        Assert.Equal("Exception", vm.Result?.DeclaringType.ShortName);
        Assert.Equal("ToString", vm.Result?.Name);
    }

    [Fact]
    public void SelectMethodDialogDimsMethodsTheClassAlreadyOverrides()
    {
        SelectMethodDialogViewModel vm = ExceptionDialog("ToString");

        Assert.Equal(["ToString"], vm.List.Rows.Where(row => row.IsOverridden).Select(row => row.Item?.Name));

        vm.List.Filter = "tostr";
        Assert.Null(vm.List.Selected);
        Assert.False(vm.List.PickCommand.CanExecute(null));
    }

    [Fact]
    public void SelectMethodDialogClosesWithNothingOnCancel()
    {
        SelectMethodDialogViewModel vm = ExceptionDialog();
        bool closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        vm.List.CancelCommand.Execute(null);

        Assert.True(closed);
        Assert.Null(vm.Result);
    }

    [Fact]
    public void SelectMethodDialogCannotPickWithNoMethods()
    {
        var vm = new SelectMethodDialogViewModel([]);

        Assert.Null(vm.List.Selected);
        Assert.False(vm.List.PickCommand.CanExecute(null));
    }

    [Fact]
    public void SelectTypeDialogResolvesSelectedItemOverTypedText()
    {
        TypeSpecifier[] types = [TypeSpecifier.FromType<object>(), StringType, TypeSpecifier.FromType<int>()];
        var vm = new SelectTypeDialogViewModel(types, TypeSpecifier.FromType<object>());

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
        var vm = new SelectTypeDialogViewModel([StringType], StringType);
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
        var vm = new TrustDialogViewModel("/work/P.csproj", ["/work/ext-a"]);
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
    public void IssuesDialogViewModelFormatsEachDiagnosticAsIdColonMessage()
    {
        var first = new CodeDiagnostic(CodeDiagnosticSeverity.Error, "NPD001", "boom", null, null, null, null, null);
        var second = new CodeDiagnostic(CodeDiagnosticSeverity.Warning, "NPD002", "careful", null, null, null, null, null);

        var vm = new IssuesDialogViewModel([first, second]);

        Assert.Equal(["NPD001: boom", "NPD002: careful"], vm.Issues);
    }

    [Fact]
    public void ErrorDialogViewModelExposesTheMessageAsGiven()
    {
        var vm = new ErrorDialogViewModel("details\nline 2");

        Assert.Equal("details\nline 2", vm.Message);
    }
}
