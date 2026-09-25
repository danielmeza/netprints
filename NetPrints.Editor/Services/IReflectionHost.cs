using System.Collections.ObjectModel;
using NetPrints.Core;
using NetPrints.Reflection;

namespace NetPrints.Editor.Services;

/// <summary>
/// Owns the reflection provider for the open project and the cached list of non-static types.
/// </summary>
public interface IReflectionHost
{
    /// <summary>Current provider. Never null (an empty provider before the first load).</summary>
    IReflectionProvider Provider { get; }

    /// <summary>Non-static types for type pickers, refreshed on reload (on the UI thread).</summary>
    ReadOnlyObservableCollection<TypeSpecifier> NonStaticTypes { get; }

    /// <summary>
    /// Warnings of the last reload (for example missing assembly references).
    /// </summary>
    IReadOnlyList<string> LastWarnings { get; }

    /// <summary>Rebuilds the provider for a project off the UI thread.</summary>
    Task ReloadAsync(Project project);

    /// <summary>Raised on the UI thread after each reload.</summary>
    event EventHandler? Reloaded;
}
