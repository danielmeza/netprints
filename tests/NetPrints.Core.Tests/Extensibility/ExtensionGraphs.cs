using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using Xunit;

namespace NetPrints.Tests.Extensibility;

/// <summary>Builds and writes small classes that use the test extension's <c>Log</c> node.</summary>
internal static class ExtensionGraphs
{
    /// <summary>A public class whose static <c>Main</c> is entry, the extension's Log node fed by the literal "hello", return.</summary>
    public static (ClassGraph Class, Node Log) BuildLogClass(ExtensionRegistry registry, string ns, string name)
    {
        Project project = TestProjects.Create(ns, ns);
        var cls = new ClassGraph { Name = name, Namespace = ns, Visibility = MemberVisibility.Public, Project = project };
        project.Classes.Add(cls);
        var method = new MethodGraph("Main") { Class = cls, Visibility = MemberVisibility.Public, Modifiers = MethodModifiers.Static };
        cls.Methods.Add(method);

        NodeKindDescriptor kind = registry.NodeKinds.Single(k => k.Kind == "netprints.test/Log");
        var log = (Node)(Activator.CreateInstance(kind.NodeType, method) ?? throw new InvalidOperationException("Log node not created."));
        LiteralNode literal = LiteralNode.WithValue(method, "hello");

        GraphUtil.ConnectExecPins(((MethodEntryNode)method.EntryNode).InitialExecutionPin, log.InputExecPins[0]);
        GraphUtil.ConnectExecPins(log.OutputExecPins[0], ((ReturnNode)method.MainReturnNode).ReturnPin);
        GraphUtil.ConnectDataPins(literal.OutputDataPins[0], log.InputDataPins[0]);
        return (cls, log);
    }

    public static JsonDocumentFormat Format(ExtensionRegistry registry) =>
        new(new NetPrintsJsonOptions(registry.NodeConverters), new DocumentMigrator([], NullLogger<DocumentMigrator>.Instance));

    public static async Task<string> WriteAsync(ExtensionRegistry registry, ClassGraph cls, string path)
    {
        ClassDocument document = new DocumentMapper(registry.NodeConverters, NullLogger<DocumentMapper>.Instance).ToDocument(cls);
        await using FileStream output = File.Create(path);
        await Format(registry).WriteClassAsync(document, output, TestContext.Current.CancellationToken);
        return path;
    }
}
