#nullable enable
using System;
using System.Linq;

namespace NetPrints.Core;

/// <summary>
/// Computes and resolves the stable "graph key" every <see cref="NodeGraph"/> of a class is
/// identified by (document-format.md §1.4.1): <c>class</c> for the class graph, a member id for a
/// method, constructor or event graph, and <c>&lt;variableId&gt;/type</c>, <c>/get</c>, <c>/set</c>
/// for a variable's type graph and accessors. Used for the document's <c>layout</c> map, diagnostic
/// locations and generator messages.
/// </summary>
public static class GraphKeys
{
    /// <summary>
    /// Returns <paramref name="graph"/>'s graph key.
    /// </summary>
    /// <param name="graph">Graph to compute the key of.</param>
    /// <returns>The graph key.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="graph"/> is not attached to a
    /// class (<see cref="NodeGraph.Class"/> is <see langword="null"/>), or is a graph kind with no
    /// defined key.</exception>
    public static string For(NodeGraph graph)
    {
        if (graph is ClassGraph)
        {
            return "class";
        }

        if (graph is TypeGraph typeGraph)
        {
            ClassGraph owner = typeGraph.OwningClass ?? throw new InvalidOperationException(
                "Graph of type 'TypeGraph' is not attached to a class.");

            foreach (Variable variable in owner.Variables)
            {
                if (ReferenceEquals(variable.TypeGraph, graph))
                {
                    return $"{variable.Id}/type";
                }
            }

            throw new InvalidOperationException("Graph of type 'TypeGraph' has no graph key.");
        }

        ClassGraph cls = graph.Class ?? throw new InvalidOperationException(
            $"Graph of type '{graph.GetType()}' is not attached to a class.");

        if (graph is MethodGraph method)
        {
            foreach (Variable variable in cls.Variables)
            {
                if (ReferenceEquals(variable.GetterMethod, method))
                {
                    return $"{variable.Id}/get";
                }

                if (ReferenceEquals(variable.SetterMethod, method))
                {
                    return $"{variable.Id}/set";
                }
            }

            return method.Id;
        }

        if (graph is ConstructorGraph constructor)
        {
            return constructor.Id;
        }

        throw new InvalidOperationException($"Graph of type '{graph.GetType()}' has no graph key.");
    }

    /// <summary>
    /// Returns the graph of <paramref name="cls"/> whose graph key is <paramref name="key"/>.
    /// </summary>
    /// <param name="cls">Class to resolve the key against.</param>
    /// <param name="key">Graph key, as returned by <see cref="For"/>.</param>
    /// <returns>The matching graph, or <see langword="null"/> if <paramref name="key"/> does not
    /// resolve to one of <paramref name="cls"/>'s graphs.</returns>
    public static NodeGraph? Resolve(ClassGraph cls, string key)
    {
        if (key == "class")
        {
            return cls;
        }

        int slash = key.IndexOf('/');
        if (slash >= 0)
        {
            string variableId = key[..slash];
            string accessor = key[(slash + 1)..];
            Variable? variable = cls.Variables.FirstOrDefault(v => v.Id == variableId);

            if (variable is null)
            {
                return null;
            }

            return accessor switch
            {
                "type" => variable.TypeGraph,
                "get" => variable.GetterMethod,
                "set" => variable.SetterMethod,
                _ => null,
            };
        }

        foreach (MethodGraph method in cls.Methods)
        {
            if (method.Id == key)
            {
                return method;
            }
        }

        foreach (ConstructorGraph constructor in cls.Constructors)
        {
            if (constructor.Id == key)
            {
                return constructor;
            }
        }

        return null;
    }
}
