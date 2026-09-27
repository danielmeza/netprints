using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Serialization.Stores;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary>
    /// The guarantees the model's <c>[OnDeserialized]</c> hooks give a loaded graph (T063 part B),
    /// pinned on the JSON path: the all-nodes fixture goes through <see cref="ProjectPersistence.LoadAsync"/>
    /// on a temp copy, so the tests survive the hooks' removal. See implementation-notes.md
    /// "T063 part B: hook guarantees" for the hook-by-hook table.
    /// </summary>
    public sealed class DeserializationGuaranteeTests : IDisposable
    {
        private const string FixtureFile = "AllNodes.Everything.netpc.json";

        private readonly string root = Directory.CreateTempSubdirectory("netprints-dg-").FullName;

        public void Dispose()
        {
            try
            { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
        }

        private sealed class FixedSnapshotProjectSystem(ProjectSnapshot snapshot) : IProjectSystem
        {
            public Task<ProjectSnapshot> LoadAsync(string projectFilePath, CancellationToken cancellationToken) =>
                Task.FromResult(snapshot);

            public Task<ProjectSnapshot> ApplyAsync(string projectFilePath, IReadOnlyList<ProjectEdit> edits, CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<string> CreateAsync(string directory, string projectName, IProjectProfile profile, string rootNamespace, CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<BuildResult> BuildAsync(string projectFilePath, CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public ProcessStartRequest GetRunCommand(string projectFilePath) => throw new NotSupportedException();
        }

        private async Task<Project> LoadAsync(params string[] fixtureFiles)
        {
            string fixtureDirectory = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "tests", "NetPrints.Core.Tests", "Fixtures", "AllNodes");
            var graphFiles = new List<string>();
            foreach (string fixtureFile in fixtureFiles)
            {
                string copy = Path.Combine(root, fixtureFile);
                File.Copy(Path.Combine(fixtureDirectory, fixtureFile), copy);
                graphFiles.Add(copy);
            }

            string projectFile = Path.Combine(root, "AllNodes.csproj");
            var snapshot = new ProjectSnapshot(projectFile, "AllNodes", "AllNodes", "AllNodes", BinaryType.SharedLibrary,
                "net10.0", DefaultProjectProfile.ProfileId, ReferencesNetPrintsSdk: true,
                GraphFiles: graphFiles, ExtensionFolders: [], References: [], DeclaredReferences: [],
                OtherSources: [], CompilationOptionsJson: "{}", Properties: new Dictionary<string, string>(), Messages: []);

            var registry = new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []);
            var persistence = new ProjectPersistence(
                new FixedSnapshotProjectSystem(snapshot),
                new DocumentFormatRegistry([new JsonDocumentFormat(new NetPrintsJsonOptions(registry), new DocumentMigrator([]))]),
                new DocumentMapper(registry),
                dir => new FileSystemDocumentStore(dir, Scheduler.Default, NullLogger<FileSystemDocumentStore>.Instance),
                NullLogger<ProjectPersistence>.Instance);

            ProjectLoadResult result = await persistence.LoadAsync(projectFile, TestContext.Current.CancellationToken);
            Assert.Empty(result.Issues);
            return result.Project;
        }

        private async Task<ClassGraph> LoadClassAsync() => (await LoadAsync(FixtureFile)).Classes.Single();

        private static IEnumerable<NodeGraph> GraphsOf(ClassGraph cls)
        {
            yield return cls;

            foreach (Variable variable in cls.Variables)
            {
                yield return variable.TypeGraph;

                if (variable.GetterMethod is { } getter)
                {
                    yield return getter;
                }

                if (variable.SetterMethod is { } setter)
                {
                    yield return setter;
                }
            }

            foreach (MethodGraph method in cls.Methods)
            {
                yield return method;
            }

            foreach (ConstructorGraph constructor in cls.Constructors)
            {
                yield return constructor;
            }
        }

        private static IEnumerable<NodePin> PinsOf(Node node) => node.InputDataPins.Cast<NodePin>()
            .Concat(node.OutputDataPins).Concat(node.InputExecPins).Concat(node.OutputExecPins)
            .Concat(node.InputTypePins).Concat(node.OutputTypePins);

        // Node.OnDeserializing (Node.cs): an input type pin's incoming pin's inferred type is
        // subscribed, so a change upstream reaches this node's HandleInputTypeChanged exactly once.
        [Fact]
        public async Task LoadedNodeReactsOnceToUpstreamTypeChange()
        {
            ClassGraph cls = await LoadClassAsync();
            TernaryNode ternary = cls.Methods.Single().Nodes.OfType<TernaryNode>().Single();
            Assert.Equal(TypeSpecifier.FromType<int>(), ternary.Type);

            int raised = 0;
            ternary.InputTypeChanged += (_, _) => raised++;

            ObservableValue<BaseType> upstream = Assert.IsType<NodeOutputTypePin>(ternary.TypePin.IncomingPin).InferredType;
            upstream.Value = TypeSpecifier.FromType<string>();

            Assert.Equal(TypeSpecifier.FromType<string>(), ternary.Type);
            Assert.Equal(TypeSpecifier.FromType<string>(), ternary.OutputObjectPin.PinType.Value);
            Assert.Equal(1, raised);
        }

        // Node.OnDeserializing: an input type pin's IncomingPinChanged handler is wired, so
        // reconnecting it re-subscribes to the new source and drops the old one.
        [Fact]
        public async Task LoadedNodeRewiresInferenceWhenTypePinIsReconnected()
        {
            ClassGraph cls = await LoadClassAsync();
            MethodGraph method = cls.Methods.Single();
            TernaryNode ternary = method.Nodes.OfType<TernaryNode>().Single();
            ExplicitCastNode cast = method.Nodes.OfType<ExplicitCastNode>().Single();
            NodeOutputTypePin oldSource = Assert.IsType<NodeOutputTypePin>(ternary.TypePin.IncomingPin);
            NodeOutputTypePin newSource = Assert.IsType<NodeOutputTypePin>(cast.CastTypePin.IncomingPin);

            GraphUtil.ConnectTypePins(newSource, ternary.TypePin);
            Assert.Equal(TypeSpecifier.FromType<string>(), ternary.Type);

            int raised = 0;
            ternary.InputTypeChanged += (_, _) => raised++;

            oldSource.InferredType.Value = TypeSpecifier.FromType<double>();
            Assert.Equal(0, raised);
            Assert.Equal(TypeSpecifier.FromType<string>(), ternary.Type);

            newSource.InferredType.Value = TypeSpecifier.FromType<bool>();
            Assert.Equal(1, raised);
            Assert.Equal(TypeSpecifier.FromType<bool>(), ternary.Type);
        }

        // MethodGraph.OnDeserialized (MethodGraph.cs): GraphTypeInference.Relax ran, so inferred pin
        // types are settled (DocumentMapper calls it on the JSON path).
        [Fact]
        public async Task LoadedGraphsHaveSettledPinTypes()
        {
            ClassGraph cls = await LoadClassAsync();
            TernaryNode ternary = cls.Methods.Single().Nodes.OfType<TernaryNode>().Single();
            Assert.Equal(TypeSpecifier.FromType<int>(), ternary.TrueObjectPin.PinType.Value);
            Assert.Equal(TypeSpecifier.FromType<int>(), ternary.FalseObjectPin.PinType.Value);
            Assert.Equal(TypeSpecifier.FromType<int>(), ternary.OutputObjectPin.PinType.Value);

            Variable items = cls.Variables.Single();
            TypeSpecifier listType = items.Type;
            Assert.Equal("System.Collections.Generic.List", listType.Name);
            Assert.Equal("T", Assert.Single(listType.GenericArguments).ToString());

            foreach (NodeGraph graph in GraphsOf(cls))
            {
                Dictionary<NodePin, BaseType?> before = TypesOf(graph);
                GraphTypeInference.Relax(graph);
                Dictionary<NodePin, BaseType?> after = TypesOf(graph);
                Assert.All(before, entry => Assert.Equal(entry.Value, after[entry.Key]));
            }
        }

        // CallMethodNode.OnMethodDeserialized (reached through MethodGraph.OnDeserialized's Relax): the
        // exception output pin exists exactly when the catch exec pin is connected, after a real load.
        [Fact]
        public async Task LoadedCallMethodNodeHasExceptionPinExactlyWhenCatchIsConnected()
        {
            Project project = await LoadAsync(FixtureFile);
            ClassGraph cls = project.Classes.Single();
            MethodGraph method = cls.Methods.Single();
            CallMethodNode call = method.Nodes.OfType<CallMethodNode>().Single();
            Assert.Null(call.ExceptionPin);

            GraphUtil.ConnectExecPins(Assert.IsType<NodeOutputExecPin>(call.CatchPin), method.ReturnNodes.First().InputExecPins[0]);
            Assert.NotNull(call.ExceptionPin);

            var registry = new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []);
            var mapper = new DocumentMapper(registry);
            var issues = new List<DocumentIssue>();
            ClassGraph withCatch = mapper.FromDocument(mapper.ToDocument(cls), project, issues, new DocumentId(FixtureFile));
            Assert.Empty(issues);
            CallMethodNode reloaded = withCatch.Methods.Single().Nodes.OfType<CallMethodNode>().Single();
            Assert.NotNull(reloaded.CatchPin?.OutgoingPin);
            Assert.Equal(TypeSpecifier.FromType<Exception>(), Assert.IsType<NodeOutputDataPin>(reloaded.ExceptionPin).PinType.Value);
        }

        private sealed record LateInferNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins)
            : NodeDocument(Id, Name, Pins);

        // Derives its output type only in OnMethodDeserialized, with no event handler: what a
        // GraphTypeInference.Relax pass exists to run once the mapper has wired the connections.
        private sealed class LateInferNode : Node
        {
            public LateInferNode(NodeGraph graph)
                : base(graph)
            {
                AddInputTypePin("Type");
                AddOutputDataPin("Out", TypeSpecifier.FromType<object>());
            }

            public NodeOutputDataPin Out => OutputDataPins[0];

            public override void OnMethodDeserialized() =>
                Out.PinType.Value = InputTypePins[0].InferredType?.Value ?? TypeSpecifier.FromType<object>();
        }

        private sealed class LateInferNodeConverter : INodeDocumentConverter
        {
            public string Kind => "test/lateInfer";
            public Type NodeType => typeof(LateInferNode);
            public Type DocumentType => typeof(LateInferNodeDocument);

            public NodeDocument ToDocument(Node node, NodeMappingContext context) => throw new NotSupportedException();

            public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context) => new LateInferNode(graph);
        }

        // DocumentMapper's two GraphTypeInference.Relax calls (MapGraphFromDocument): the constructors'
        // events settle every built-in node, so only a node that infers in OnMethodDeserialized alone
        // shows that the mapper runs the pass after wiring the connections.
        [Fact]
        public async Task MapperRunsTypeInferenceAfterWiringConnections()
        {
            Project project = await LoadAsync(FixtureFile);
            var registry = new NodeDocumentConverterRegistry([.. NodeDocumentConverterRegistry.BuiltIn, new LateInferNodeConverter()], []);
            var mapper = new DocumentMapper(registry);

            ClassDocument document = mapper.ToDocument(project.Classes.Single());
            Assert.NotNull(document.Methods);
            MethodDocument main = document.Methods.Single();
            Assert.NotNull(main.Graph.Connections);
            const string lateId = "n000000001pxzz";
            GraphDocument graph = main.Graph with
            {
                Nodes = [.. main.Graph.Nodes, new LateInferNodeDocument(lateId, null, null)],
                Connections = [.. main.Graph.Connections, new ConnectionDocument("n000000001pxre/out.type.OutputType", $"{lateId}/in.type.Type")],
            };
            document = document with { Methods = [main with { Graph = graph }] };

            var issues = new List<DocumentIssue>();
            ClassGraph loaded = mapper.FromDocument(document, project, issues, new DocumentId(FixtureFile));

            Assert.Empty(issues);
            var late = Assert.IsType<LateInferNode>(loaded.Methods.Single().FindNode(lateId));
            Assert.Equal(TypeSpecifier.FromType<int>(), late.Out.PinType.Value);
        }

        private static Dictionary<NodePin, BaseType?> TypesOf(NodeGraph graph)
        {
            var types = new Dictionary<NodePin, BaseType?>();
            foreach (Node node in graph.Nodes)
            {
                foreach (NodeDataPin pin in node.InputDataPins.Cast<NodeDataPin>().Concat(node.OutputDataPins))
                {
                    types[pin] = pin.PinType.Value;
                }

                foreach (NodeTypePin pin in node.InputTypePins.Cast<NodeTypePin>().Concat(node.OutputTypePins))
                {
                    types[pin] = pin.InferredType?.Value;
                }
            }

            return types;
        }

        // Variable.OnDeserialized (Variable.cs): Class and TypeGraph.OwningClass are set and TypeGraph
        // is non-null, so GraphKeys.For(variable.TypeGraph) resolves "<variable id>/type".
        [Fact]
        public async Task LoadedVariableReferencesItsClassAndKeysItsTypeGraph()
        {
            Project project = await LoadAsync(FixtureFile);
            ClassGraph cls = project.Classes.Single();
            Variable variable = cls.Variables.Single();

            Assert.Same(cls, variable.Class);
            Assert.NotNull(variable.TypeGraph);
            Assert.Same(cls, variable.TypeGraph.OwningClass);
            Assert.Same(project, variable.TypeGraph.Project);
            Assert.Equal($"{variable.Id}/type", GraphKeys.For(variable.TypeGraph));
            Assert.Equal($"{variable.Id}/get", GraphKeys.For(Assert.IsType<MethodGraph>(variable.GetterMethod)));
            Assert.Equal($"{variable.Id}/set", GraphKeys.For(Assert.IsType<MethodGraph>(variable.SetterMethod)));
            Assert.Same(cls, variable.GetterMethod.Class);
            Assert.Same(cls, variable.SetterMethod.Class);
        }

        // Project.FixDefaults (Project.cs): Classes is a non-null collection, in graph-file order,
        // and usable when the project has no graph files.
        [Fact]
        public async Task LoadedProjectHasUsableClassesCollection()
        {
            Project empty = await LoadAsync();
            Assert.NotNull(empty.Classes);
            Assert.Empty(empty.Classes);

            ClassGraph added = empty.CreateNewClass(DefaultProjectProfile.Instance);
            Assert.Same(added, Assert.Single(empty.Classes));

            Project loaded = await LoadAsync(FixtureFile);
            Assert.Equal("AllNodes.Everything", Assert.Single(loaded.Classes).FullName);
        }

        // NodeGraph's id index (NodeGraph.cs): every node of every loaded graph is found by the id the
        // document gave it, and the index tracks later additions and removals.
        [Fact]
        public async Task LoadedGraphsFindEveryNodeByItsDocumentId()
        {
            ClassGraph cls = await LoadClassAsync();

            foreach (NodeGraph graph in GraphsOf(cls))
            {
                Assert.All(graph.Nodes, node => Assert.Same(node, graph.FindNode(node.Id)));
                Assert.Null(graph.FindNode("n-no-such-node"));
            }

            TypeGraph typeGraph = cls.Variables.Single().TypeGraph;
            Assert.IsType<TypeNode>(typeGraph.FindNode("n000000001pxr2"));

            var added = new TypeNode(typeGraph, TypeSpecifier.FromType<int>());
            Assert.Same(added, typeGraph.FindNode(added.Id));
            typeGraph.Nodes.Remove(added);
            Assert.Null(typeGraph.FindNode(added.Id));
        }

        // Owner back-references DataContract restored and the mapper must now wire: node.Graph,
        // pin.Node, graph.Class/Project, and connection symmetry.
        [Fact]
        public async Task LoadedModelHasConsistentBackReferences()
        {
            Project project = await LoadAsync(FixtureFile);
            ClassGraph cls = project.Classes.Single();

            Assert.Same(project, cls.Project);

            foreach (NodeGraph graph in GraphsOf(cls))
            {
                Assert.Same(project, graph.Project);

                if (graph is not ClassGraph && graph is not TypeGraph)
                {
                    Assert.Same(cls, graph.Class);
                }

                foreach (Node node in graph.Nodes)
                {
                    Assert.Same(graph, node.Graph);
                    Assert.All(PinsOf(node), pin => Assert.Same(node, pin.Node));

                    foreach (NodeOutputExecPin pin in node.OutputExecPins)
                    {
                        if (pin.OutgoingPin is { } target)
                        {
                            Assert.Contains(pin, target.IncomingPins);
                        }
                    }

                    foreach (NodeOutputDataPin pin in node.OutputDataPins)
                    {
                        Assert.All(pin.OutgoingPins, target => Assert.Same(pin, target.IncomingPin));
                    }

                    foreach (NodeInputDataPin pin in node.InputDataPins)
                    {
                        if (pin.IncomingPin is { } source)
                        {
                            Assert.Contains(pin, source.OutgoingPins);
                        }
                    }

                    foreach (NodeOutputTypePin pin in node.OutputTypePins)
                    {
                        Assert.All(pin.OutgoingPins, target => Assert.Same(pin, target.IncomingPin));
                    }
                }
            }
        }

        // Stored state only: a read-only computed property (MakeDelegateNode.TargetPin) may throw by design.
        private static bool IsStoredProperty(PropertyInfo property) =>
            property.CanRead
            && !property.PropertyType.IsValueType
            && property.GetIndexParameters().Length == 0
            && (property.GetSetMethod(nonPublic: true) is not null || property.GetMethod?.IsDefined(typeof(CompilerGeneratedAttribute)) == true);

        // The "cached field null after deserialization" bug class: DataContract skipped constructors
        // and initializers, leaving these properties null. Every non-nullable reference and every
        // collection property of a loaded model object must have a value.
        [Fact]
        public async Task LoadedModelObjectsHaveNoNullNonNullableProperties()
        {
            Project project = await LoadAsync(FixtureFile);
            ClassGraph cls = project.Classes.Single();

            var subjects = new List<object> { project, cls };
            subjects.AddRange(cls.Variables);
            foreach (NodeGraph graph in GraphsOf(cls))
            {
                subjects.Add(graph);
                subjects.AddRange(graph.Nodes);
                subjects.AddRange(graph.Nodes.SelectMany(PinsOf));
            }

            var nullProperties = new SortedSet<string>(StringComparer.Ordinal);
            var context = new NullabilityInfoContext();
            foreach (object subject in subjects)
            {
                foreach (PropertyInfo property in subject.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!IsStoredProperty(property))
                    {
                        continue;
                    }

                    bool isCollection = property.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(property.PropertyType);
                    NullabilityState state = context.Create(property).ReadState;
                    if (state != NullabilityState.NotNull && !(isCollection && state == NullabilityState.Unknown))
                    {
                        continue;
                    }

                    if (property.GetValue(subject) is null)
                    {
                        nullProperties.Add($"{subject.GetType().Name}.{property.Name}");
                    }
                }
            }

            Assert.Empty(nullProperties);
        }
    }
}
