using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Pre-resolves the overload and constructor lists of the call and constructor nodes of a project's graphs on a background
/// thread, so the reflection provider's memoized cache is warm when a graph's node view models ask for the same lists on
/// the UI thread while the canvas is built.
/// </summary>
internal static class OverloadWarmUp
{
    /// <summary>Warms the overload lists of every graph of <paramref name="project"/>.</summary>
    /// <param name="reflection">The reflection host; nothing happens until it has a provider.</param>
    /// <param name="project">The project whose graphs are scanned (on the calling thread).</param>
    /// <param name="cancellationToken">Stops the background work.</param>
    /// <returns>A task that completes when the queries are done.</returns>
    public static async Task WarmAsync(IReflectionHost reflection, Project project, CancellationToken cancellationToken)
    {
        if (!reflection.IsLoaded)
        {
            return;
        }

        List<MethodSpecifier> methods = [];
        List<TypeSpecifier> constructorTypes = [];
        foreach (Node node in project.Classes.SelectMany(GraphsOf).SelectMany(graph => graph.Nodes))
        {
            switch (node)
            {
                case CallMethodNode { MethodSpecifier: { } method }:
                    methods.Add(method);
                    break;
                case ConstructorNode { ConstructorSpecifier: { } constructor }:
                    constructorTypes.Add(constructor.DeclaringType);
                    break;
            }
        }

        if (methods.Count == 0 && constructorTypes.Count == 0)
        {
            return;
        }

        var provider = reflection.Provider;
        await Task.Run(() =>
        {
            foreach (MethodSpecifier method in methods)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = provider.GetPublicMethodOverloads(method).Count();
            }

            foreach (TypeSpecifier type in constructorTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = provider.GetConstructors(type).Count();
            }
        }, cancellationToken).ConfigureAwait(true);
    }

    private static IEnumerable<NodeGraph> GraphsOf(ClassGraph cls)
    {
        yield return cls;

        foreach (MethodGraph method in cls.Methods)
        {
            yield return method;
        }

        foreach (ConstructorGraph constructor in cls.Constructors)
        {
            yield return constructor;
        }

        foreach (EventGraph eventGraph in cls.EventGraphs)
        {
            yield return eventGraph;
        }

        foreach (Variable variable in cls.Variables)
        {
            if (variable.GetterMethod is { } getter)
            {
                yield return getter;
            }

            if (variable.SetterMethod is { } setter)
            {
                yield return setter;
            }

            yield return variable.TypeGraph;
        }
    }
}
