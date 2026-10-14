using NetPrints.Core;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.UndoRedo;

namespace NetPrints.Editor.Tests.Inspectors;

/// <summary>Visibility and modifier edits of the class, method and variable inspectors are one undo step each.</summary>
public sealed class MemberPropertyUndoTests : IDisposable
{
    private readonly ClassContext context;
    private readonly ClassGraph cls;

    public MemberPropertyUndoTests(TestEditor editor)
    {
        cls = new ClassGraph { Name = "C", Namespace = "N" };
        context = new ClassContext(cls, editor.Context, new UndoRedoStack());
    }

    public void Dispose() => context.Dispose();

    private void AssertUndoable<T>(Func<T> read, T original, T edited, Action edit)
    {
        context.UndoRedo.MarkSaved();
        cls.MarkClean();

        edit();

        Assert.Equal(edited, read());
        Assert.True(cls.IsDirty);

        Assert.True(context.UndoRedo.Undo());
        Assert.Equal(original, read());
        Assert.False(cls.IsDirty);

        Assert.True(context.UndoRedo.Redo());
        Assert.Equal(edited, read());
        Assert.True(cls.IsDirty);

        Assert.True(context.UndoRedo.Undo());
        Assert.Equal(original, read());
        Assert.False(cls.IsDirty);
    }

    [Theory]
    [InlineData(nameof(VariableModifiers.ReadOnly), VariableModifiers.ReadOnly)]
    [InlineData(nameof(VariableModifiers.Static), VariableModifiers.Static)]
    [InlineData(nameof(VariableModifiers.New), VariableModifiers.New)]
    public void AVariableModifierEditIsOneUndoStep(string name, VariableModifiers flag)
    {
        context.CreateVariable();
        var variable = context.Variables.Single();

        AssertUndoable(() => variable.Modifiers, VariableModifiers.None, flag, () =>
        {
            switch (name)
            {
                case nameof(VariableModifiers.ReadOnly):
                    variable.IsReadOnly = true;
                    break;
                case nameof(VariableModifiers.Static):
                    variable.IsStatic = true;
                    break;
                default:
                    variable.IsNew = true;
                    break;
            }
        });
    }

    [Fact]
    public void AVariableVisibilityEditIsOneUndoStepAndRestoresTheFollowingAccessors()
    {
        context.CreateVariable();
        var variable = context.Variables.Single();
        variable.AddGetterCommand.Execute(null);
        variable.AddSetterCommand.Execute(null);
        MethodGraph getter = Assert.IsType<MethodGraph>(variable.Getter);
        MethodGraph setter = Assert.IsType<MethodGraph>(variable.Setter);
        setter.Visibility = MemberVisibility.Public;
        MemberVisibility original = variable.Visibility;

        AssertUndoable(() => variable.Visibility, original, MemberVisibility.Protected, () => variable.Visibility = MemberVisibility.Protected);

        Assert.Equal(original, getter.Visibility);
        Assert.Equal(MemberVisibility.Public, setter.Visibility);
        context.UndoRedo.Redo();
        Assert.Equal(MemberVisibility.Protected, getter.Visibility);
        Assert.Equal(MemberVisibility.Public, setter.Visibility);
    }

    [Fact]
    public void AMethodVisibilityEditIsOneUndoStep()
    {
        var method = context.CreateMethod();
        var viewModel = context.Methods.Single(m => m.Graph == method);

        AssertUndoable(() => viewModel.Visibility, method.Visibility, MemberVisibility.Internal, () => viewModel.Visibility = MemberVisibility.Internal);
    }

    [Fact]
    public void AConstructorVisibilityEditIsOneUndoStep()
    {
        var constructor = context.CreateConstructor();
        var viewModel = context.Constructors.Single(c => c.Graph == constructor);

        AssertUndoable(() => viewModel.Visibility, constructor.Visibility, MemberVisibility.Internal, () => viewModel.Visibility = MemberVisibility.Internal);
    }

    [Theory]
    [InlineData(MethodModifiers.Sealed)]
    [InlineData(MethodModifiers.Abstract)]
    [InlineData(MethodModifiers.Static)]
    [InlineData(MethodModifiers.Virtual)]
    [InlineData(MethodModifiers.Override)]
    [InlineData(MethodModifiers.Async)]
    public void AMethodModifierEditIsOneUndoStep(MethodModifiers flag)
    {
        var method = context.CreateMethod();
        method.Modifiers = MethodModifiers.None;
        var viewModel = context.Methods.Single(m => m.Graph == method);

        AssertUndoable(() => method.Modifiers, MethodModifiers.None, flag, () =>
        {
            switch (flag)
            {
                case MethodModifiers.Sealed:
                    viewModel.IsSealed = true;
                    break;
                case MethodModifiers.Abstract:
                    viewModel.IsAbstract = true;
                    break;
                case MethodModifiers.Static:
                    viewModel.IsStatic = true;
                    break;
                case MethodModifiers.Virtual:
                    viewModel.IsVirtual = true;
                    break;
                case MethodModifiers.Override:
                    viewModel.IsOverride = true;
                    break;
                default:
                    viewModel.IsAsync = true;
                    break;
            }
        });
    }

    [Fact]
    public void AClassVisibilityEditIsOneUndoStep()
    {
        AssertUndoable(() => cls.Visibility, cls.Visibility, MemberVisibility.Public, () => context.ClassInspector.Visibility = MemberVisibility.Public);
    }

    [Theory]
    [InlineData(ClassModifiers.Sealed)]
    [InlineData(ClassModifiers.Abstract)]
    [InlineData(ClassModifiers.Static)]
    [InlineData(ClassModifiers.Partial)]
    public void AClassModifierEditIsOneUndoStep(ClassModifiers flag)
    {
        cls.Modifiers = ClassModifiers.None;

        AssertUndoable(() => cls.Modifiers, ClassModifiers.None, flag, () =>
        {
            switch (flag)
            {
                case ClassModifiers.Sealed:
                    context.ClassInspector.IsSealed = true;
                    break;
                case ClassModifiers.Abstract:
                    context.ClassInspector.IsAbstract = true;
                    break;
                case ClassModifiers.Static:
                    context.ClassInspector.IsStatic = true;
                    break;
                default:
                    context.ClassInspector.IsPartial = true;
                    break;
            }
        });
    }
}
