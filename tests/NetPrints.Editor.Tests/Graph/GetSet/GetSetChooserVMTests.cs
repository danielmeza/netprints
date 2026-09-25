using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Graph;

namespace NetPrints.Editor.Tests.Graph.GetSet;

public class GetSetChooserVMTests(TestEditor editor) : GraphTestBase(editor)
{
    [Fact]
    public void EnablesAccordingToVisibilityAndCreatesNodes()
    {
        var chooser = Graph.GetSetChooser;
        var publicGetPrivateSet = new VariableSpecifier("Value", IntType, MemberVisibility.Public, MemberVisibility.Private,
            TypeSpecifier.FromType<Version>(), VariableModifiers.None);

        chooser.Open(publicGetPrivateSet, new NetPrints.Editor.Graph.GraphPoint(28, 28));
        Assert.True(chooser.IsOpen);
        Assert.True(chooser.CanGet);
        Assert.False(chooser.CanSet, "a private setter of another type is not visible");
        Assert.False(chooser.SetCommand.CanExecute(null));

        chooser.GetCommand.Execute(null);
        var getter = Method.Nodes.OfType<VariableGetterNode>().Single();
        Assert.Equal(28, getter.PositionX);
        Assert.False(chooser.IsOpen);

        var own = new VariableSpecifier("Own", IntType, MemberVisibility.Private, MemberVisibility.Private, Class.Type, VariableModifiers.None);
        chooser.Open(own, new NetPrints.Editor.Graph.GraphPoint(0, 0));
        Assert.True(chooser.CanSet, "own private members are visible");
        chooser.SetCommand.Execute(null);
        Assert.Equal(1, Method.Nodes.OfType<VariableSetterNode>().ToList().Count());

        chooser.Open(own, new NetPrints.Editor.Graph.GraphPoint(0, 0));
        chooser.CloseCommand.Execute(null);
        Assert.False(chooser.IsOpen, "closes when the pointer leaves");
    }
}
