using System.Collections.ObjectModel;

namespace NetPrints.Extensibility.Testing;

/// <summary>
/// Declares what an <see cref="ExtensionTest{TExtension}"/> loads and exercises: the extension folders, the id the extension under
/// test is loaded under and the node kinds to place in a class.
/// </summary>
public sealed class TestState
{
    /// <summary>The id the extension under test is loaded under when <see cref="ExtensionId"/> is not set.</summary>
    public const string DefaultExtensionId = "test.extension";

    /// <summary>Gets the folders of folder-based extensions to load next to the extension under test.</summary>
    public Collection<string> ExtensionFolders { get; } = [];

    /// <summary>
    /// Gets or sets the manifest id of the extension under test. The node kinds it contributes start with this id and a slash.
    /// </summary>
    public string ExtensionId { get; set; } = DefaultExtensionId;

    /// <summary>Gets the node kinds to place, in order, in the class the test translates and saves. Empty means no class is built.</summary>
    public Collection<string> NodeKinds { get; } = [];
}
