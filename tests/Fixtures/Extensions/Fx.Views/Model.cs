namespace Fx.Views;

/// <summary>A view model of the extension; its template is in <c>Templates.axaml</c>.</summary>
public sealed class Model
{
    /// <summary>Gets the children a tree template shows.</summary>
    public IReadOnlyList<Model> Children { get; } = [];
}
