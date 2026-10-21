#nullable enable

namespace NetPrints.Graph
{
    /// <summary>
    /// A node whose pin names the user edits in place (parameters and return values). The editor asks for this
    /// role instead of testing node types.
    /// </summary>
    public interface IPinNameEditor
    {
        /// <summary>
        /// Whether the user can rename <paramref name="pin"/>.
        /// </summary>
        /// <param name="pin">A pin of this node.</param>
        /// <returns><see langword="true"/> if the pin's name is editable.</returns>
        bool CanRenamePin(NodePin pin);
    }
}
