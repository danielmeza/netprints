using System.Collections;
using NetPrints.Extensibility.Loading;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// The MSBuild property names <c>ProjectSystemOptions.ExtraProperties</c> asks the project system to evaluate: always the
/// names of the extensions loaded right now (<see cref="ExtensionRegistry.ProjectProperties"/>), so a project system created
/// once sees the properties of extensions loaded later (FR-025).
/// </summary>
public sealed class ExtensionProjectProperties : IReadOnlyList<string>
{
    private readonly IExtensionHost extensions;

    /// <summary>Creates the live list over <paramref name="extensions"/>.</summary>
    /// <param name="extensions">The extension host whose current registry supplies the names.</param>
    public ExtensionProjectProperties(IExtensionHost extensions)
    {
        this.extensions = extensions ?? throw new ArgumentNullException(nameof(extensions));
    }

    /// <inheritdoc/>
    public int Count => extensions.Current.ProjectProperties.Count;

    /// <inheritdoc/>
    public string this[int index] => extensions.Current.ProjectProperties[index];

    /// <inheritdoc/>
    public IEnumerator<string> GetEnumerator() => extensions.Current.ProjectProperties.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
