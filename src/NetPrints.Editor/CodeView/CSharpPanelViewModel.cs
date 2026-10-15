using NetPrints.Core;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.CodeView;

/// <summary>The C# panel: the generated code of the active document's class (<see cref="ActiveClassPanelViewModel{TItem}.Current"/>).</summary>
public sealed class CSharpPanelViewModel : ActiveClassPanelViewModel<CodeViewViewModel>
{
    /// <inheritdoc/>
    protected override CodeViewViewModel CreateItem(ClassGraph cls, PanelContext context) => new(cls, context.Context.CodeAnalysis);
}
