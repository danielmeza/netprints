namespace Fx.Views;

/// <summary>A view model of the extension; its tree template is in <c>Templates.axaml</c>.</summary>
public sealed class Model
{
    /// <summary>Gets the children the tree template shows.</summary>
    public IList<Model> Children { get; } = [];
}
