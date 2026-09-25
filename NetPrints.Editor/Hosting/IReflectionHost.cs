using System.Collections.ObjectModel;
using NetPrints.Core;
using NetPrints.Reflection;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Owns the reflection provider for the open project and the cached list of non-static types.
/// </summary>
/// <remarks>
/// "Not loaded yet" is an explicit state (<see cref="IsLoaded"/>), not an empty provider:
/// consumers show nothing until the first load and refresh on <see cref="Reloaded"/>.
/// </remarks>
public interface IReflectionHost
{
    /// <summary>Whether a provider has been loaded.</summary>
    bool IsLoaded { get; }

    /// <summary>Completes when the first provider has been loaded.</summary>
    Task Loaded { get; }

    /// <summary>The current provider. Throws <see cref="InvalidOperationException"/> before the first load.</summary>
    IReflectionProvider Provider { get; }

    /// <summary>Non-static types for type pickers, refreshed on reload (on the UI thread).</summary>
    ReadOnlyObservableCollection<TypeSpecifier> NonStaticTypes { get; }

    /// <summary>
    /// Warnings of the last reload (for example missing assembly references).
    /// </summary>
    IReadOnlyList<string> LastWarnings { get; }

    /// <summary>Rebuilds the provider for a project off the UI thread.</summary>
    Task ReloadAsync(Project project, CancellationToken cancellationToken = default);

    /// <summary>Raised on the UI thread after each reload.</summary>
    event EventHandler? Reloaded;
}
