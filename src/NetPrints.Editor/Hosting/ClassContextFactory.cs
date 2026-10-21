using Microsoft.Extensions.DependencyInjection;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UndoRedo;

namespace NetPrints.Editor.Hosting;

/// <summary>Creates the <see cref="ClassContext"/> of a class and the graph scope it works on (research R4 steps 4 and 5).</summary>
[Singleton<ClassContextFactory>]
[ScopeFactory]
public sealed partial class ClassContextFactory
{
    private readonly IServiceProvider services;

    /// <summary>Creates the factory over <paramref name="services"/>.</summary>
    /// <param name="services">The container that resolves the host services.</param>
    public ClassContextFactory(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        this.services = services;
    }

    /// <summary>Creates the factory for a host with no container, which resolves only <paramref name="context"/> and the graph scope factory.</summary>
    /// <param name="context">Host services shared across the editor.</param>
    /// <returns>The factory.</returns>
    public static ClassContextFactory For(EditorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new ClassContextFactory(new ContextOnlyServices(context));
    }

    /// <summary>Creates the context of <paramref name="cls"/> with its own graph scope.</summary>
    /// <param name="cls">The class.</param>
    /// <param name="undoRedo">The class's undo stack; every applied command marks the class dirty.</param>
    /// <param name="projectClasses">The classes whose nodes a rename must reach; defaults to <paramref name="cls"/> alone.</param>
    /// <returns>The class's context.</returns>
    public ClassContext Create(ClassGraph cls, UndoRedoStack undoRedo, Func<IReadOnlyList<ClassGraph>>? projectClasses = null) =>
        CreateContext(cls, CreateScope(cls, undoRedo, projectClasses));

    /// <summary>Creates the graph scope of <paramref name="cls"/>: its services, with a messenger of its own.</summary>
    /// <param name="cls">The class.</param>
    /// <param name="undoRedo">The class's undo stack.</param>
    /// <param name="projectClasses">The classes whose nodes a rename must reach; defaults to <paramref name="cls"/> alone.</param>
    /// <returns>The services of the scope.</returns>
    public ClassEditorServices CreateScope(ClassGraph cls, UndoRedoStack undoRedo, Func<IReadOnlyList<ClassGraph>>? projectClasses = null)
    {
        ArgumentNullException.ThrowIfNull(cls);
        ArgumentNullException.ThrowIfNull(undoRedo);
        EditorContext context = services.GetRequiredService<EditorContext>();
        return services.GetRequiredService<GraphScopeFactory>().CreateServices(undoRedo, context.CreateMessenger(), projectClasses ?? (() => [cls]));
    }

    /// <summary>Creates the context of <paramref name="cls"/> on an existing graph scope.</summary>
    /// <param name="cls">The class.</param>
    /// <param name="scope">The services of the class's graph scope.</param>
    /// <returns>The class's context.</returns>
    public partial ClassContext CreateContext(ClassGraph cls, ClassEditorServices scope);

    private sealed class ContextOnlyServices(EditorContext context) : IServiceProvider
    {
        private GraphScopeFactory? scopes;

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(EditorContext))
            {
                return context;
            }

            return serviceType == typeof(GraphScopeFactory) ? scopes ??= new GraphScopeFactory(this) : null;
        }
    }
}
