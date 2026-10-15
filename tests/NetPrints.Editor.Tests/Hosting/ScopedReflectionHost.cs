using System.Collections.ObjectModel;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Projects;
using NetPrints.Reflection;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>
/// One test's view of the run-wide reflection host of <see cref="Startup"/>: every member forwards to the
/// shared host except <see cref="Reloaded"/>, whose subscribers stay here and are dropped when the test's
/// scope disposes this host. A graph or analysis host a test leaves open then goes with the test instead of
/// staying subscribed to the shared host, with its editor and compilation, until the run ends.
/// </summary>
public sealed class ScopedReflectionHost : IReflectionHost, IDisposable
{
    private readonly IReflectionHost shared;
    private bool disposed;

    public ScopedReflectionHost(IReflectionHost shared)
    {
        ArgumentNullException.ThrowIfNull(shared);
        this.shared = shared;
        shared.Reloaded += OnSharedReloaded;
    }

    public bool IsLoaded => shared.IsLoaded;

    public Task Loaded => shared.Loaded;

    public IReflectionProvider Provider => shared.Provider;

    public ProjectSnapshot? Snapshot => shared.Snapshot;

    public ReadOnlyObservableCollection<TypeSpecifier> NonStaticTypes => shared.NonStaticTypes;

    public IReadOnlyList<string> LastWarnings => shared.LastWarnings;

    public event EventHandler? Reloaded;

    public Task ReloadAsync(Project project, CancellationToken cancellationToken = default) => shared.ReloadAsync(project, cancellationToken);

    /// <summary>Unsubscribes from the shared host and drops this test's <see cref="Reloaded"/> subscribers.</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        shared.Reloaded -= OnSharedReloaded;
        Reloaded = null;
    }

    private void OnSharedReloaded(object? sender, EventArgs e) => Reloaded?.Invoke(this, e);
}
