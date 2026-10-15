using NetPrints.Core;
using NetPrints.Editor.Controls;

namespace NetPrints.Editor.Tests.Controls;

public class MethodPickerListViewModelTests
{
    private static MethodPickerItem Item(Type declaring, string name, int parameters = 0, bool current = false, bool overridden = false, bool isAbstract = false)
    {
        MethodParameter[] arguments = [.. Enumerable.Range(0, parameters).Select(i => new MethodParameter($"p{i}", TypeSpecifier.FromType<int>(), MethodParameterPassType.Default, false, null))];
        var method = new MethodSpecifier(name, arguments, [TypeSpecifier.FromType<string>()], isAbstract ? MethodModifiers.Abstract : MethodModifiers.Virtual,
            MemberVisibility.Public, TypeSpecifier.FromType(declaring), []);
        return MethodPickerItem.For(method, current, overridden);
    }

    private static string[] Texts(MethodPickerListViewModel list) => [.. list.Rows.Select(row => row.IsHeader ? $"# {row.Text}" : row.Text)];

    private static MethodPickerListViewModel Realistic() => new(
    [
        Item(typeof(Exception), "ToString"),
        Item(typeof(Exception), "GetBaseException"),
        Item(typeof(object), "Equals", 1),
        Item(typeof(object), "ToString"),
        Item(typeof(object), "Equals", 2),
        Item(typeof(object), "Finalize", overridden: true),
    ]);

    [Fact]
    public void RowsAreOneFlatListOfHeadersAndMethodsInTheCallersGroupOrder()
    {
        MethodPickerListViewModel list = Realistic();

        Assert.Equal(
        [
            "# Exception", "string GetBaseException()", "string ToString()",
            "# Object", "string Equals(int p0)", "string Equals(int p0, int p1)", "string Finalize()", "string ToString()",
        ], Texts(list));
    }

    [Fact]
    public void GroupsKeepTheOrderTheCallerGivesEvenWhenItIsNotAlphabetical()
    {
        var list = new MethodPickerListViewModel([Item(typeof(object), "A"), Item(typeof(Exception), "B"), Item(typeof(object), "C")]);

        Assert.Equal(["# Object", "# Exception"], Texts(list).Where(t => t.StartsWith('#')));
    }

    [Fact]
    public void MethodsInAGroupSortByNameThenParameterCount()
    {
        var list = new MethodPickerListViewModel([Item(typeof(object), "B", 2), Item(typeof(object), "A", 3), Item(typeof(object), "B", 1), Item(typeof(object), "a", 0)]);

        Assert.Equal(["a", "A", "B", "B"], list.Rows.Where(r => !r.IsHeader).Select(r => r.Item?.Name));
        Assert.Equal([0, 3, 1, 2], list.Rows.Where(r => !r.IsHeader).Select(r => r.Item?.ParameterCount ?? -1));
    }

    [Fact]
    public void TheCurrentRowComesFirstAndIsMarked()
    {
        var list = new MethodPickerListViewModel([Item(typeof(object), "A"), Item(typeof(object), "Z", current: true), Item(typeof(object), "B")]);

        MethodPickerRow first = list.Rows[1];
        Assert.True(first.IsCurrent);
        Assert.Equal("Z", first.Item?.Name);
        Assert.Single(list.Rows, row => row.IsCurrent);
    }

    [Fact]
    public void HeadersCanBeLeftOutForASingleTypeList()
    {
        var list = new MethodPickerListViewModel([Item(typeof(object), "A"), Item(typeof(object), "B")], showGroupHeaders: false);

        Assert.DoesNotContain(list.Rows, row => row.IsHeader);
        Assert.Equal(2, list.Rows.Count);
    }

    [Fact]
    public void FilterMatchesTheNameOrTheSignatureIgnoringCaseAndDropsEmptyGroups()
    {
        MethodPickerListViewModel list = Realistic();

        list.Filter = "tostr";
        Assert.Equal(["# Exception", "string ToString()", "# Object", "string ToString()"], Texts(list));

        list.Filter = "INT P0, INT";
        Assert.Equal(["# Object", "string Equals(int p0, int p1)"], Texts(list));

        list.Filter = "getbase";
        Assert.Equal(["# Exception", "string GetBaseException()"], Texts(list));

        list.Filter = "no such thing";
        Assert.Empty(list.Rows);
        Assert.True(list.IsEmpty);

        list.Filter = "";
        Assert.Equal(8, list.Rows.Count);
        Assert.False(list.IsEmpty);
    }

    [Fact]
    public void TheFirstPickableRowIsSelectedAfterEachFilterChange()
    {
        MethodPickerListViewModel list = Realistic();
        Assert.Equal("GetBaseException", list.Selected?.Item?.Name);

        list.Filter = "tostr";
        Assert.Equal("Exception", list.Selected?.Item?.DeclaringTypeName);
        Assert.Equal("ToString", list.Selected?.Item?.Name);

        list.Filter = "zzz";
        Assert.Null(list.Selected);
    }

    [Fact]
    public void HeaderRowsCannotBeSelected()
    {
        MethodPickerListViewModel list = Realistic();
        MethodPickerRow before = list.Selected ?? throw new InvalidOperationException("Nothing selected.");

        list.Selected = list.Rows[0];

        Assert.Same(before, list.Selected);
    }

    [Fact]
    public void AnOverriddenRowIsDimmedAndCannotBePicked()
    {
        MethodPickerListViewModel list = Realistic();
        MethodPickerRow overridden = Assert.Single(list.Rows, row => row.IsOverridden);
        MethodPickerRow before = list.Selected ?? throw new InvalidOperationException("Nothing selected.");

        list.Selected = overridden;

        Assert.False(overridden.IsPickable);
        Assert.Same(before, list.Selected);

        list.Filter = "finalize";
        Assert.Null(list.Selected);
        Assert.False(list.PickCommand.CanExecute(null));
    }

    [Fact]
    public void AnAbstractRowIsMarked()
    {
        var list = new MethodPickerListViewModel([Item(typeof(object), "A", isAbstract: true), Item(typeof(object), "B")]);

        Assert.Equal([true, false], list.Rows.Where(r => !r.IsHeader).Select(r => r.IsAbstract));
    }

    [Fact]
    public void PickIsDisabledWithNoPickableSelectionAndReportsThePickOnce()
    {
        MethodPickerListViewModel list = Realistic();
        var picked = new List<MethodPickerItem>();
        list.Picked += (_, item) => picked.Add(item);

        list.Filter = "zzz";
        Assert.False(list.PickCommand.CanExecute(null));

        list.Filter = "tostr";
        list.Selected = list.Rows.Last(row => !row.IsHeader);
        Assert.True(list.PickCommand.CanExecute(null));
        list.PickCommand.Execute(null);

        MethodPickerItem item = Assert.Single(picked);
        Assert.Equal("Object", item.DeclaringTypeName);
        Assert.Equal("ToString", item.Name);
    }

    [Fact]
    public void CancelReportsACancel()
    {
        MethodPickerListViewModel list = Realistic();
        int cancels = 0;
        list.Cancelled += (_, _) => cancels++;

        list.CancelCommand.Execute(null);

        Assert.Equal(1, cancels);
    }

    [Fact]
    public void AConstructorRowShowsItsTypeNameAndParameters()
    {
        var constructor = new ConstructorSpecifier([new MethodParameter("capacity", TypeSpecifier.FromType<int>(), MethodParameterPassType.Default, false, null)],
            TypeSpecifier.FromType<System.Text.StringBuilder>());

        var list = new MethodPickerListViewModel([MethodPickerItem.For(constructor)]);

        Assert.Equal(["# StringBuilder", "StringBuilder(int capacity)"], Texts(list));
        Assert.Same(constructor, list.Rows[1].Item?.Member);
    }
}
