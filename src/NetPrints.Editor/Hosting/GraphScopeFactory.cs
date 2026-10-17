using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.UndoRedo;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Creates the services one class's graphs, rows, nodes and pins share (<see cref="ClassEditorServices"/>). The container resolves
/// the host services once here; the view models below receive the result and resolve nothing themselves (research R4 step 5).
/// </summary>
[Singleton<GraphScopeFactory>]
[ScopeFactory]
public sealed partial class GraphScopeFactory
{
    private readonly IServiceProvider services;

    /// <summary>Creates the factory over <paramref name="services"/>.</summary>
    /// <param name="services">The container that resolves the host services.</param>
    public GraphScopeFactory(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        this.services = services;
    }

    /// <summary>Creates the services of one class's graph scope.</summary>
    /// <param name="undoRedo">The class's undo stack.</param>
    /// <param name="messenger">The messenger scoped to the class.</param>
    /// <param name="projectClasses">The classes whose nodes a rename must reach.</param>
    /// <returns>The services, with the host services of the container.</returns>
    public partial ClassEditorServices CreateServices(UndoRedoStack undoRedo, IMessenger messenger, Func<IReadOnlyList<ClassGraph>> projectClasses);
}
