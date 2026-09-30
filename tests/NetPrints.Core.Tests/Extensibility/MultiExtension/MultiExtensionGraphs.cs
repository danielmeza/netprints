using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Extensibility.Loading;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Mapping;
using Xunit;

namespace NetPrints.Tests.Extensibility.MultiExtension;

/// <summary>Builds small classes whose <c>Main</c> runs one node of each given kind in order.</summary>
internal static class MultiExtensionGraphs
{
    /// <summary>A public class with a static <c>Main</c>: entry, then a node of each kind in <paramref name="kinds"/>, return.</summary>
    public static ClassGraph BuildClass(ExtensionRegistry registry, string ns, string name, params string[] kinds)
    {
        Project project = TestProjects.Create(ns, ns);
        var cls = new ClassGraph { Name = name, Namespace = ns, Visibility = MemberVisibility.Public, Project = project };
        project.Classes.Add(cls);
        var method = new MethodGraph("Main") { Class = cls, Visibility = MemberVisibility.Public, Modifiers = MethodModifiers.Static };
        cls.Methods.Add(method);

        NodeOutputExecPin previous = ((MethodEntryNode)method.EntryNode).InitialExecutionPin;
        foreach (string kind in kinds)
        {
            var descriptor = registry.NodeKinds.Single(k => k.Kind == kind);
            Node node = descriptor.Suggestions.Single().Create(method);
            GraphUtil.ConnectExecPins(previous, node.InputExecPins[0]);
            previous = node.OutputExecPins[0];
        }

        GraphUtil.ConnectExecPins(previous, ((ReturnNode)method.MainReturnNode).ReturnPin);
        return cls;
    }

    /// <summary>The canonical document bytes of <paramref name="cls"/>, written with <paramref name="registry"/>'s converters.</summary>
    public static async Task<byte[]> WriteAsync(ExtensionRegistry registry, ClassGraph cls)
    {
        ClassDocument document = new DocumentMapper(registry.NodeConverters, NullLogger<DocumentMapper>.Instance).ToDocument(cls);
        await using var output = new MemoryStream();
        await ExtensionGraphs.Format(registry).WriteClassAsync(document, output, TestContext.Current.CancellationToken);
        return output.ToArray();
    }
}
