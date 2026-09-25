using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.ViewModels;

[TestClass]
public class GetSetChooserVMTests : GraphTestBase
{
    [TestMethod]
    public void EnablesAccordingToVisibilityAndCreatesNodes()
    {
        var chooser = Graph.GetSetChooser;
        var publicGetPrivateSet = new VariableSpecifier("Value", IntType, MemberVisibility.Public, MemberVisibility.Private,
            TypeSpecifier.FromType<Version>(), VariableModifiers.None);

        chooser.Open(publicGetPrivateSet, new NetPrints.Editor.ViewModels.GraphPoint(28, 28));
        Assert.IsTrue(chooser.IsOpen);
        Assert.IsTrue(chooser.CanGet);
        Assert.IsFalse(chooser.CanSet, "a private setter of another type is not visible");
        Assert.IsFalse(chooser.SetCommand.CanExecute(null));

        chooser.GetCommand.Execute(null);
        var getter = Method.Nodes.OfType<VariableGetterNode>().Single();
        Assert.AreEqual(28, getter.PositionX);
        Assert.IsFalse(chooser.IsOpen);

        var own = new VariableSpecifier("Own", IntType, MemberVisibility.Private, MemberVisibility.Private, Class.Type, VariableModifiers.None);
        chooser.Open(own, new NetPrints.Editor.ViewModels.GraphPoint(0, 0));
        Assert.IsTrue(chooser.CanSet, "own private members are visible");
        chooser.SetCommand.Execute(null);
        Assert.HasCount(1, Method.Nodes.OfType<VariableSetterNode>().ToList());

        chooser.Open(own, new NetPrints.Editor.ViewModels.GraphPoint(0, 0));
        chooser.CloseCommand.Execute(null);
        Assert.IsFalse(chooser.IsOpen, "closes when the pointer leaves");
    }
}
