#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;

namespace NetPrints.Serialization.Mapping;

/// <summary>
/// A graph's nodes and connections a converter could not recreate (an unknown or untrusted node
/// kind), kept so a re-save (or a save of the rest of the class) does not lose them
/// (document-format.md §2.6). Stored as <see cref="NodeGraph.PreservedDocumentState"/>.
/// </summary>
/// <param name="Nodes">Nodes of unrecognized kind, unchanged.</param>
/// <param name="Connections">Connections that reference at least one of <paramref name="Nodes"/>.</param>
internal sealed record PreservedGraphState(IReadOnlyList<UnknownNodeDocument> Nodes, IReadOnlyList<ConnectionDocument> Connections);

/// <summary>
/// Maps a <see cref="ClassGraph"/> to and from its <see cref="ClassDocument"/> form (document-format.md
/// §2.6): member ids, pins by key (<see cref="PinKeys"/>), default names, edges, integer layout,
/// auto-placement of unpositioned nodes, and preserved unknown nodes.
/// </summary>
public sealed class DocumentMapper : IDocumentMapper
{
    private readonly NodeDocumentConverterRegistry nodes;

    /// <summary>
    /// Creates a mapper backed by <paramref name="nodes"/>.
    /// </summary>
    /// <param name="nodes">Node document converters (built-in and extension) to map with.</param>
    public DocumentMapper(NodeDocumentConverterRegistry nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        this.nodes = nodes;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException"><paramref name="cls"/> has two members sharing a
    /// member id (callers run <see cref="ClassGraph.EnsureUniqueMemberIds"/> first), or a node of
    /// <paramref name="cls"/>'s graphs has no registered converter.</exception>
    public ClassDocument ToDocument(ClassGraph cls)
    {
        ArgumentNullException.ThrowIfNull(cls);

        var seenMemberIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (object member in cls.Members)
        {
            if (!seenMemberIds.Add(GetMemberId(member)))
            {
                throw new InvalidOperationException($"Duplicate member id '{GetMemberId(member)}'.");
            }
        }

        var context = new NodeMappingContext(cls);

        List<VariableDocument>? variables = cls.Variables.Count > 0
            ? cls.Variables.Select(v => MapVariable(v, context)).ToList()
            : null;
        List<MethodDocument>? methods = cls.Methods.Count > 0
            ? cls.Methods.Select(m => MapMethod(m, context)).ToList()
            : null;
        List<ConstructorDocument>? constructors = cls.Constructors.Count > 0
            ? cls.Constructors.Select(c => MapConstructor(c, context)).ToList()
            : null;
        List<string>? genericArguments = cls.DeclaredGenericArguments.Count > 0
            ? cls.DeclaredGenericArguments.Select(g => g.Name).ToList()
            : null;

        return new ClassDocument(
            Migrations.DocumentMigrator.CurrentSchemaVersion,
            string.IsNullOrEmpty(cls.Namespace) ? null : cls.Namespace,
            cls.Name,
            cls.Visibility,
            cls.Modifiers,
            genericArguments,
            MapGraph(cls, context),
            variables,
            methods,
            constructors,
            null,
            BuildLayout(cls));
    }

    /// <inheritdoc/>
    /// <exception cref="DocumentFormatException">A member or node id is missing; or a layout position
    /// array does not have exactly 2 elements. A present id that does not match <see cref="IdFormat"/>
    /// for its prefix, or one duplicated within its scope of uniqueness, does not throw: it is
    /// reassigned a fresh id and reported as a <see cref="DocumentIssue"/> warning
    /// (<see cref="DocumentIssue.InvalidIdReassigned"/> or <see cref="DocumentIssue.DuplicateIdReassigned"/>,
    /// document-format.md §1.4.1, §2.6, research.md R21).</exception>
    /// <exception cref="InvalidOperationException">A node document's runtime type has no registered
    /// converter (a genuine registry gap, not a document problem — an unrecognized <c>$kind</c> never
    /// reaches this far as anything but an <see cref="UnknownNodeDocument"/>).</exception>
    public ClassGraph FromDocument(ClassDocument document, Project project, ICollection<DocumentIssue> issues, DocumentId id)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(issues);

        ValidateMemberIdShapes(document, id);

        var cls = new ClassGraph
        {
            Namespace = document.Namespace ?? string.Empty,
            Name = document.Name,
            Visibility = document.Visibility,
            Modifiers = document.Modifiers,
            Project = project,
        };

        if (document.GenericArguments is not null)
        {
            cls.DeclaredGenericArguments.AddRange(document.GenericArguments.Select(name => new GenericType(name)));
        }

        var context = new NodeMappingContext(cls);
        var knownGraphKeys = new HashSet<string>(StringComparer.Ordinal);
        var seenMemberIds = new HashSet<string>(StringComparer.Ordinal);

        MapGraphFromDocument(document.ClassGraph, cls, context, document.Layout, knownGraphKeys, id, issues);

        foreach (VariableDocument variableDocument in document.Variables ?? [])
        {
            MapVariableFromDocument(variableDocument, cls, context, document.Layout, knownGraphKeys, seenMemberIds, id, issues);
        }

        foreach (MethodDocument methodDocument in document.Methods ?? [])
        {
            MapMethodFromDocument(methodDocument, cls, context, document.Layout, knownGraphKeys, seenMemberIds, id, issues);
        }

        foreach (ConstructorDocument constructorDocument in document.Constructors ?? [])
        {
            MapConstructorFromDocument(constructorDocument, cls, context, document.Layout, knownGraphKeys, seenMemberIds, id, issues);
        }

        // EventGraphs: sub-phase G (T080) adds ClassGraph.EventGraphs and must come back here.

        if (document.Layout is not null)
        {
            foreach (string graphKey in document.Layout.Keys)
            {
                if (!knownGraphKeys.Contains(graphKey))
                {
                    issues.Add(new DocumentIssue(DocumentIssueSeverity.Info, DocumentIssue.LayoutEntryIgnored,
                        $"Layout entry for unknown graph '{graphKey}' ignored.", id));
                }
            }
        }

        return cls;
    }

    private static string GetMemberId(object member) => member switch
    {
        Variable variable => variable.Id,
        MethodGraph method => method.Id,
        ConstructorGraph constructor => constructor.Id,
        _ => throw new InvalidOperationException($"Unknown member type '{member.GetType()}'."),
    };

    private MethodDocument MapMethod(MethodGraph method, NodeMappingContext context) =>
        new(method.Id, method.Name, method.Visibility, method.Modifiers, MapGraph(method, context));

    private ConstructorDocument MapConstructor(ConstructorGraph constructor, NodeMappingContext context) =>
        new(constructor.Id, constructor.Visibility, MapGraph(constructor, context));

    private VariableDocument MapVariable(Variable variable, NodeMappingContext context)
    {
        AccessorDocument? getter = variable.GetterMethod is { } g ? new AccessorDocument(g.Visibility, MapGraph(g, context)) : null;
        AccessorDocument? setter = variable.SetterMethod is { } s ? new AccessorDocument(s.Visibility, MapGraph(s, context)) : null;

        return new VariableDocument(variable.Id, variable.Name, variable.Visibility, variable.Modifiers,
            MapGraph(variable.TypeGraph, context), getter, setter);
    }

    private GraphDocument MapGraph(NodeGraph graph, NodeMappingContext context)
    {
        var nodeDocuments = new List<NodeDocument>();

        foreach (Node node in graph.Nodes)
        {
            INodeDocumentConverter converter = nodes.FindByNodeType(node.GetType())
                ?? throw new InvalidOperationException($"No document converter registered for node type '{node.GetType()}'.");

            NodeDocument raw = converter.ToDocument(node, context);
            nodeDocuments.Add(raw with
            {
                Id = node.Id,
                Name = node.Name == node.DefaultName ? null : node.Name,
                Pins = BuildPinStates(node, context),
            });
        }

        List<ConnectionDocument> connections = BuildConnections(graph);

        if (graph.PreservedDocumentState is PreservedGraphState preserved)
        {
            nodeDocuments.AddRange(preserved.Nodes);
            connections.AddRange(preserved.Connections);
        }

        connections.Sort((a, b) =>
        {
            int cmp = string.CompareOrdinal(a.From, b.From);
            return cmp != 0 ? cmp : string.CompareOrdinal(a.To, b.To);
        });

        return new GraphDocument(nodeDocuments, connections.Count > 0 ? connections : null, null);
    }

    private static List<PinStateDocument>? BuildPinStates(Node node, NodeMappingContext context)
    {
        List<PinStateDocument>? pins = null;

        void AddState(NodePin pin, TypedValue? value)
        {
            string keyName = node.GetPinKeyName(pin);
            string? name = pin.Name == keyName ? null : pin.Name;

            if (name is null && value is null)
            {
                return;
            }

            (pins ??= []).Add(new PinStateDocument(PinKeys.For(pin), name, value));
        }

        foreach (NodeInputDataPin pin in node.InputDataPins)
        {
            TypedValue? value = null;
            if (pin.UnconnectedValue is not null)
            {
                // An enum-typed pin's UnconnectedValue is already a string (the pin setter's own
                // validation requires it); TypedValueConverter would otherwise record its runtime
                // type as System.String instead of the pin's actual enum type (implementation-notes.md
                // T028/T029).
                value = pin.PinType.Value is TypeSpecifier { IsEnum: true } enumType && pin.UnconnectedValue is string enumValue
                    ? new TypedValue(enumType.Name, enumValue)
                    : context.ToValue(pin.UnconnectedValue, $"{node.Id}/{PinKeys.For(pin)}");
            }

            AddState(pin, value);
        }

        foreach (NodeOutputDataPin pin in node.OutputDataPins)
        {
            AddState(pin, null);
        }

        foreach (NodeInputExecPin pin in node.InputExecPins)
        {
            AddState(pin, null);
        }

        foreach (NodeOutputExecPin pin in node.OutputExecPins)
        {
            AddState(pin, null);
        }

        foreach (NodeInputTypePin pin in node.InputTypePins)
        {
            AddState(pin, null);
        }

        foreach (NodeOutputTypePin pin in node.OutputTypePins)
        {
            AddState(pin, null);
        }

        return pins;
    }

    private static List<ConnectionDocument> BuildConnections(NodeGraph graph)
    {
        var connections = new List<ConnectionDocument>();

        foreach (Node node in graph.Nodes)
        {
            foreach (NodeOutputExecPin pin in node.OutputExecPins)
            {
                if (pin.OutgoingPin is { } target)
                {
                    connections.Add(new ConnectionDocument($"{node.Id}/{PinKeys.For(pin)}", $"{target.Node.Id}/{PinKeys.For(target)}"));
                }
            }

            foreach (NodeOutputDataPin pin in node.OutputDataPins)
            {
                foreach (NodeInputDataPin target in pin.OutgoingPins)
                {
                    connections.Add(new ConnectionDocument($"{node.Id}/{PinKeys.For(pin)}", $"{target.Node.Id}/{PinKeys.For(target)}"));
                }
            }

            foreach (NodeOutputTypePin pin in node.OutputTypePins)
            {
                foreach (NodeInputTypePin target in pin.OutgoingPins)
                {
                    connections.Add(new ConnectionDocument($"{node.Id}/{PinKeys.For(pin)}", $"{target.Node.Id}/{PinKeys.For(target)}"));
                }
            }
        }

        return connections;
    }

    private static SortedDictionary<string, SortedDictionary<string, int[]>>? BuildLayout(ClassGraph cls)
    {
        var layout = new SortedDictionary<string, SortedDictionary<string, int[]>>(StringComparer.Ordinal);

        foreach (NodeGraph graph in EnumerateGraphs(cls))
        {
            if (graph.Nodes.Count == 0)
            {
                continue;
            }

            var positions = new SortedDictionary<string, int[]>(StringComparer.Ordinal);
            foreach (Node node in graph.Nodes)
            {
                positions[node.Id] = [Round(node.PositionX), Round(node.PositionY)];
            }

            layout[GraphKeys.For(graph)] = positions;
        }

        return layout.Count > 0 ? layout : null;
    }

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Enumerates every graph of <paramref name="cls"/>: the class graph itself, each variable's type
    /// graph and getter/setter method graphs, and each method and constructor graph. Shared with
    /// <see cref="Legacy.LegacyXmlDocumentFormat"/>, which runs <see cref="GraphTypeInference.Relax"/>
    /// over the same set after assigning legacy node ids.
    /// </summary>
    /// <param name="cls">Class to enumerate the graphs of.</param>
    /// <returns>Every graph of <paramref name="cls"/>.</returns>
    internal static IEnumerable<NodeGraph> EnumerateGraphs(ClassGraph cls)
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

    /// <summary>
    /// Checks that every member id is present. A malformed shape is not an error here any more
    /// (research.md R21): <see cref="ResolveMemberId"/> repairs it (a fresh id, <c>NPD009</c>) once
    /// the member is actually created, the same way <see cref="ResolveDuplicateId"/> repairs a
    /// duplicate (<c>NPD007</c>) — this only guards the one case neither repair path can recover from.
    /// </summary>
    private static void ValidateMemberIdShapes(ClassDocument document, DocumentId id)
    {
        void Check(string memberId)
        {
            if (string.IsNullOrEmpty(memberId))
            {
                throw new DocumentFormatException($"Invalid member id '{memberId}'.", id);
            }
        }

        foreach (VariableDocument variable in document.Variables ?? [])
        {
            Check(variable.Id);
        }

        foreach (MethodDocument method in document.Methods ?? [])
        {
            Check(method.Id);
        }

        foreach (ConstructorDocument constructor in document.Constructors ?? [])
        {
            Check(constructor.Id);
        }
    }

    /// <summary>
    /// Returns <paramref name="documentId"/> if it is not already in <paramref name="seenIds"/>
    /// (adding it); otherwise allocates a fresh id (<see cref="StableIds.AllocateUnique"/>), adds
    /// that instead, and reports a <see cref="DocumentIssue.DuplicateIdReassigned"/> warning
    /// (document-format.md §2.6: merge safety — the document still loads).
    /// </summary>
    private static string ResolveDuplicateId(char prefix, string documentId, HashSet<string> seenIds, string what,
        DocumentId id, ICollection<DocumentIssue> issues)
    {
        if (seenIds.Add(documentId))
        {
            return documentId;
        }

        string reassigned = StableIds.AllocateUnique(prefix, seenIds);
        seenIds.Add(reassigned);
        issues.Add(new DocumentIssue(DocumentIssueSeverity.Warning, DocumentIssue.DuplicateIdReassigned,
            $"{what} '{documentId}' is duplicated; reassigned to '{reassigned}'.", id));
        return reassigned;
    }

    /// <summary>
    /// Resolves a member id: a shape not matching <see cref="IdFormat.PatternFor"/> is replaced by a
    /// fresh one first (<see cref="DocumentIssue.InvalidIdReassigned"/>, <c>NPD009</c>, research.md R21),
    /// renaming <paramref name="layout"/>'s graph-key entry (or entries, for a variable) that named the
    /// old text so the member's own graphs — mapped right after this call — still find their positions;
    /// then <see cref="ResolveDuplicateId"/> repairs a duplicate the same as before (<c>NPD007</c>).
    /// </summary>
    private static string ResolveMemberId(char prefix, string documentId, string what, HashSet<string> seenIds,
        SortedDictionary<string, SortedDictionary<string, int[]>>? layout, bool isVariable,
        DocumentId id, ICollection<DocumentIssue> issues)
    {
        string resolvedId = documentId;

        if (!IdFormat.IsValid(documentId, prefix))
        {
            resolvedId = IdGeneration.Current.NewId(prefix);
            issues.Add(new DocumentIssue(DocumentIssueSeverity.Warning, DocumentIssue.InvalidIdReassigned,
                $"{what} '{documentId}' does not match the id format; reassigned to '{resolvedId}'.", id));
            RenameLayoutGraphKeys(layout, documentId, resolvedId, isVariable);
        }

        return ResolveDuplicateId(prefix, resolvedId, seenIds, what, id, issues);
    }

    /// <summary>Renames <paramref name="layout"/>'s entry for a repaired member id (and, for a
    /// variable, its <c>/type</c>, <c>/get</c> and <c>/set</c> graph keys), leaving any entry not
    /// present untouched.</summary>
    private static void RenameLayoutGraphKeys(SortedDictionary<string, SortedDictionary<string, int[]>>? layout,
        string oldMemberId, string newMemberId, bool isVariable)
    {
        if (layout is null)
        {
            return;
        }

        RenameLayoutGraphKey(layout, oldMemberId, newMemberId);

        if (isVariable)
        {
            RenameLayoutGraphKey(layout, $"{oldMemberId}/type", $"{newMemberId}/type");
            RenameLayoutGraphKey(layout, $"{oldMemberId}/get", $"{newMemberId}/get");
            RenameLayoutGraphKey(layout, $"{oldMemberId}/set", $"{newMemberId}/set");
        }
    }

    private static void RenameLayoutGraphKey(SortedDictionary<string, SortedDictionary<string, int[]>> layout, string oldKey, string newKey)
    {
        if (layout.TryGetValue(oldKey, out SortedDictionary<string, int[]>? positions))
        {
            layout.Remove(oldKey);
            layout[newKey] = positions;
        }
    }

    /// <summary>
    /// Resolves a node id's shape: a document id not matching <see cref="IdFormat.PatternFor"/> for
    /// <c>'n'</c> is replaced by a fresh one, recorded in <paramref name="oldToNewNodeId"/> (first
    /// occurrence wins, matching <see cref="ResolveDuplicateId"/>'s own convention for a repeated raw
    /// text) so the caller can rewrite this graph's connections and layout entries that name the old
    /// text, and reported as <see cref="DocumentIssue.InvalidIdReassigned"/> (<c>NPD009</c>) — before
    /// <see cref="ResolveDuplicateId"/> repairs a duplicate of the result.
    /// </summary>
    private static string ResolveInvalidNodeId(string documentId, string what, Dictionary<string, string> oldToNewNodeId,
        DocumentId id, ICollection<DocumentIssue> issues)
    {
        if (IdFormat.IsValid(documentId, 'n'))
        {
            return documentId;
        }

        string reassigned = IdGeneration.Current.NewId('n');
        oldToNewNodeId.TryAdd(documentId, reassigned);
        issues.Add(new DocumentIssue(DocumentIssueSeverity.Warning, DocumentIssue.InvalidIdReassigned,
            $"{what} '{documentId}' does not match the id format; reassigned to '{reassigned}'.", id));
        return reassigned;
    }

    /// <summary>Remaps every connection endpoint whose node id was repaired by
    /// <see cref="ResolveInvalidNodeId"/>; returns <paramref name="connections"/> unchanged if nothing
    /// needed repair.</summary>
    private static IReadOnlyList<ConnectionDocument>? RemapConnectionEndpoints(IReadOnlyList<ConnectionDocument>? connections,
        IReadOnlyDictionary<string, string> oldToNewNodeId)
    {
        if (connections is null || oldToNewNodeId.Count == 0)
        {
            return connections;
        }

        return connections
            .Select(connection => new ConnectionDocument(RemapEndpoint(connection.From, oldToNewNodeId), RemapEndpoint(connection.To, oldToNewNodeId)))
            .ToList();
    }

    private static string RemapEndpoint(string endpoint, IReadOnlyDictionary<string, string> oldToNewNodeId)
    {
        (string nodeId, string pinRef) = SplitEndpoint(endpoint);
        return oldToNewNodeId.TryGetValue(nodeId, out string? replacement) ? $"{replacement}/{pinRef}" : endpoint;
    }

    private void MapVariableFromDocument(VariableDocument document, ClassGraph cls, NodeMappingContext context,
        SortedDictionary<string, SortedDictionary<string, int[]>>? layout, HashSet<string> knownGraphKeys,
        HashSet<string> seenMemberIds, DocumentId id, ICollection<DocumentIssue> issues)
    {
        var variable = new Variable(cls, document.Name, TypeSpecifier.FromType<object>(), null, null, document.Modifiers)
        {
            Visibility = document.Visibility,
        };
        variable.Id = ResolveMemberId('m', document.Id, "Member", seenMemberIds, layout, isVariable: true, id, issues);

        // Variable's constructor always builds a placeholder type-node tree for its TypeSpecifier
        // argument (GraphUtil.CreateNestedTypeNode), unlike every other graph kind's constructor,
        // which only ever creates its one fixed node. Strip that placeholder back to just the fixed
        // TypeReturnNode so the document's own type-graph nodes are the only ones left to add
        // (implementation-notes.md T035, the Variable.TypeGraph special case).
        TypeGraph typeGraph = variable.TypeGraph;
        typeGraph.Project = cls.Project;
        typeGraph.ReturnNode.TypePin.IncomingPin = null;
        foreach (Node placeholder in typeGraph.Nodes.Where(n => n is not TypeReturnNode).ToList())
        {
            typeGraph.Nodes.Remove(placeholder);
        }

        // Added to cls.Variables (and GetterMethod/SetterMethod assigned below) before its graphs are
        // mapped: GraphKeys.For resolves a variable's type/getter/setter graphs by finding the owning
        // Variable via reference equality, which requires these back-references to already be wired.
        cls.Variables.Add(variable);

        MapGraphFromDocument(document.TypeGraph, typeGraph, context, layout, knownGraphKeys, id, issues);

        if (document.Getter is not null)
        {
            var getter = new MethodGraph($"get_{document.Name}") { Class = cls, Project = cls.Project, Visibility = document.Getter.Visibility };
            variable.GetterMethod = getter;
            MapGraphFromDocument(document.Getter.Graph, getter, context, layout, knownGraphKeys, id, issues);
        }

        if (document.Setter is not null)
        {
            var setter = new MethodGraph($"set_{document.Name}") { Class = cls, Project = cls.Project, Visibility = document.Setter.Visibility };
            variable.SetterMethod = setter;
            MapGraphFromDocument(document.Setter.Graph, setter, context, layout, knownGraphKeys, id, issues);
        }
    }

    private void MapMethodFromDocument(MethodDocument document, ClassGraph cls, NodeMappingContext context,
        SortedDictionary<string, SortedDictionary<string, int[]>>? layout, HashSet<string> knownGraphKeys,
        HashSet<string> seenMemberIds, DocumentId id, ICollection<DocumentIssue> issues)
    {
        var method = new MethodGraph(document.Name)
        {
            Class = cls,
            Project = cls.Project,
            Visibility = document.Visibility,
            Modifiers = document.Modifiers,
        };
        method.Id = ResolveMemberId('m', document.Id, "Member", seenMemberIds, layout, isVariable: false, id, issues);
        cls.Methods.Add(method);

        MapGraphFromDocument(document.Graph, method, context, layout, knownGraphKeys, id, issues);
    }

    private void MapConstructorFromDocument(ConstructorDocument document, ClassGraph cls, NodeMappingContext context,
        SortedDictionary<string, SortedDictionary<string, int[]>>? layout, HashSet<string> knownGraphKeys,
        HashSet<string> seenMemberIds, DocumentId id, ICollection<DocumentIssue> issues)
    {
        var constructor = new ConstructorGraph { Class = cls, Project = cls.Project, Visibility = document.Visibility };
        constructor.Id = ResolveMemberId('m', document.Id, "Member", seenMemberIds, layout, isVariable: false, id, issues);
        cls.Constructors.Add(constructor);

        MapGraphFromDocument(document.Graph, constructor, context, layout, knownGraphKeys, id, issues);
    }

    private void MapGraphFromDocument(GraphDocument graphDocument, NodeGraph graph, NodeMappingContext context,
        SortedDictionary<string, SortedDictionary<string, int[]>>? layout, HashSet<string> knownGraphKeys,
        DocumentId id, ICollection<DocumentIssue> issues)
    {
        string graphKey = GraphKeys.For(graph);
        knownGraphKeys.Add(graphKey);

        var preservedNodes = new List<UnknownNodeDocument>();
        var seenNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var createdNodes = new List<(Node Node, NodeDocument Document)>();

        // A node id not matching IdFormat is replaced first (research.md R21, NPD009); oldToNewNodeId
        // records the replacement (first occurrence wins for a repeated raw text, matching
        // ResolveDuplicateId's own convention) so the connections and layout entries below — which
        // still name the old text — can be found under the new one.
        var oldToNewNodeId = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (NodeDocument nodeDocument in graphDocument.Nodes)
        {
            if (nodeDocument is UnknownNodeDocument unknown)
            {
                preservedNodes.Add(unknown);
                issues.Add(new DocumentIssue(DocumentIssueSeverity.Warning, DocumentIssue.UnknownNodeKind,
                    $"Node '{unknown.Id}' has unknown kind '{unknown.Kind}'; preserved unchanged.", id));
                continue;
            }

            if (string.IsNullOrEmpty(nodeDocument.Id))
            {
                throw new DocumentFormatException($"Invalid node id '{nodeDocument.Id}' in graph '{graphKey}'.", id);
            }

            INodeDocumentConverter converter = nodes.FindByDocumentType(nodeDocument.GetType())
                ?? throw new InvalidOperationException($"No node document converter registered for document type '{nodeDocument.GetType()}'.");

            Node node = converter.CreateNode(nodeDocument, graph, context);
            string previousId = node.Id;

            string documentNodeId = ResolveInvalidNodeId(nodeDocument.Id, $"Node in graph '{graphKey}'", oldToNewNodeId, id, issues);
            node.Id = ResolveDuplicateId('n', documentNodeId, seenNodeIds, $"Node in graph '{graphKey}'", id, issues);
            graph.ReindexNode(node, previousId);
            node.Name = nodeDocument.Name ?? node.DefaultName;
            createdNodes.Add((node, nodeDocument));
        }

        var preservedNodeIds = new HashSet<string>(preservedNodes.Select(n => n.Id), StringComparer.Ordinal);
        var preservedConnections = new List<ConnectionDocument>();
        ApplyConnections(RemapConnectionEndpoints(graphDocument.Connections, oldToNewNodeId), graph, id, issues, preservedNodeIds, preservedConnections);

        if (preservedNodes.Count > 0 || preservedConnections.Count > 0)
        {
            graph.PreservedDocumentState = new PreservedGraphState(preservedNodes, preservedConnections);
        }

        // Settle types from the connections just wired before applying pin states: a pin's
        // eligibility for an unconnected value (and, for an enum pin, the shape the value must have)
        // can depend on a type-pin connection resolved above rather than on the node's constructor
        // (e.g. TernaryNode.TrueObjectPin's PinType tracks its own TypePin's incoming connection).
        GraphTypeInference.Relax(graph);

        foreach ((Node node, NodeDocument nodeDocument) in createdNodes)
        {
            ApplyPinStates(node, nodeDocument.Pins, context, id, issues);
        }

        SortedDictionary<string, int[]>? positions = layout?.GetValueOrDefault(graphKey);
        IReadOnlyDictionary<string, int[]>? positionsByCurrentId = positions;

        if (positions is not null && oldToNewNodeId.Count > 0)
        {
            // positions' own keys still name the pre-repair text; re-key by each node's current id so
            // the lookups below (and the "unknown node" check) find a repaired node under it.
            var remapped = new Dictionary<string, int[]>(positions.Count, StringComparer.Ordinal);
            foreach ((string rawNodeId, int[] xy) in positions)
            {
                remapped.TryAdd(oldToNewNodeId.GetValueOrDefault(rawNodeId, rawNodeId), xy);
            }

            positionsByCurrentId = remapped;
        }

        var unpositioned = new List<Node>();

        foreach (Node node in graph.Nodes)
        {
            if (positionsByCurrentId is not null && positionsByCurrentId.TryGetValue(node.Id, out int[]? xy))
            {
                if (xy.Length != 2)
                {
                    throw new DocumentFormatException(
                        $"Layout position for node '{node.Id}' in graph '{graphKey}' must have exactly 2 elements.", id);
                }

                node.PositionX = xy[0];
                node.PositionY = xy[1];
            }
            else
            {
                unpositioned.Add(node);
            }
        }

        if (positionsByCurrentId is not null)
        {
            foreach (string nodeId in positionsByCurrentId.Keys)
            {
                if (graph.FindNode(nodeId) is null)
                {
                    issues.Add(new DocumentIssue(DocumentIssueSeverity.Info, DocumentIssue.LayoutEntryIgnored,
                        $"Layout entry for unknown node '{nodeId}' in graph '{graphKey}' ignored.", id));
                }
            }
        }

        if (unpositioned.Count > 0)
        {
            GraphAutoLayout.PlaceUnpositioned(graph, unpositioned);
        }

        GraphTypeInference.Relax(graph);
    }

    private static void ApplyPinStates(Node node, IReadOnlyList<PinStateDocument>? pins, NodeMappingContext context,
        DocumentId id, ICollection<DocumentIssue> issues)
    {
        if (pins is null)
        {
            return;
        }

        foreach (PinStateDocument pinState in pins)
        {
            NodePin? pin = PinKeys.Find(node, pinState.Pin);
            if (pin is null)
            {
                issues.Add(new DocumentIssue(DocumentIssueSeverity.Warning, DocumentIssue.PinStateDropped,
                    $"Node '{node.Id}' has no pin '{pinState.Pin}'.", id));
                continue;
            }

            if (pinState.Name is not null)
            {
                pin.Name = pinState.Name;
            }

            if (pinState.Value is not null && pin is NodeInputDataPin inputDataPin)
            {
                // Symmetric with BuildPinStates: an enum-typed pin's UnconnectedValue is the raw
                // string itself, never routed through TypedValueConverter.
                inputDataPin.UnconnectedValue = inputDataPin.PinType.Value is TypeSpecifier { IsEnum: true }
                    ? pinState.Value.Value
                    : context.FromValue(pinState.Value);
            }
        }
    }

    private static void ApplyConnections(IReadOnlyList<ConnectionDocument>? connections, NodeGraph graph, DocumentId id,
        ICollection<DocumentIssue> issues, IReadOnlySet<string> preservedNodeIds, List<ConnectionDocument> preservedConnections)
    {
        if (connections is null)
        {
            return;
        }

        foreach (ConnectionDocument connection in connections)
        {
            (string fromNodeId, string fromPinRef) = SplitEndpoint(connection.From);
            (string toNodeId, string toPinRef) = SplitEndpoint(connection.To);

            Node? fromNode = graph.FindNode(fromNodeId);
            Node? toNode = graph.FindNode(toNodeId);

            if (fromNode is null || toNode is null)
            {
                if ((fromNode is null && preservedNodeIds.Contains(fromNodeId)) || (toNode is null && preservedNodeIds.Contains(toNodeId)))
                {
                    preservedConnections.Add(connection);
                }
                else
                {
                    issues.Add(new DocumentIssue(DocumentIssueSeverity.Warning, DocumentIssue.ConnectionDropped,
                        $"Connection '{connection.From}' -> '{connection.To}' references a missing node.", id));
                }

                continue;
            }

            NodePin? fromPin = PinKeys.Find(fromNode, fromPinRef);
            NodePin? toPin = PinKeys.Find(toNode, toPinRef);

            if (fromPin is null || toPin is null)
            {
                issues.Add(new DocumentIssue(DocumentIssueSeverity.Warning, DocumentIssue.ConnectionDropped,
                    $"Connection '{connection.From}' -> '{connection.To}' references an unknown pin.", id));
                continue;
            }

            try
            {
                switch ((fromPin, toPin))
                {
                    case (NodeOutputExecPin fromExec, NodeInputExecPin toExec):
                        GraphUtil.ConnectExecPins(fromExec, toExec);
                        break;
                    case (NodeOutputDataPin fromData, NodeInputDataPin toData):
                        GraphUtil.ConnectDataPins(fromData, toData);
                        break;
                    case (NodeOutputTypePin fromType, NodeInputTypePin toType):
                        GraphUtil.ConnectTypePins(fromType, toType);
                        break;
                    default:
                        issues.Add(new DocumentIssue(DocumentIssueSeverity.Warning, DocumentIssue.ConnectionDropped,
                            $"Connection '{connection.From}' -> '{connection.To}' connects incompatible pins.", id));
                        break;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                issues.Add(new DocumentIssue(DocumentIssueSeverity.Warning, DocumentIssue.ConnectionDropped,
                    $"Connection '{connection.From}' -> '{connection.To}' could not be connected: {ex.Message}", id));
            }
        }
    }

    private static (string NodeId, string PinReference) SplitEndpoint(string endpoint)
    {
        int slash = endpoint.IndexOf('/');
        return slash < 0 ? (endpoint, string.Empty) : (endpoint[..slash], endpoint[(slash + 1)..]);
    }
}
