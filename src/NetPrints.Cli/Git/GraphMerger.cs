using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;

namespace NetPrints.Cli.Git;

/// <summary>
/// Merges two versions of a class graph that diverged from a common base by identity (contracts/git.md §2): class fields three-way,
/// members by id, nodes by id, pins by key, connections as set differences, layout with ours winning. The merged document is validated;
/// anything that cannot be merged or is invalid after the merge is reported as a <see cref="MergeConflict"/>.
/// </summary>
/// <param name="format">The document format used to compare documents: two pieces of a document are equal when they write the same bytes.</param>
internal sealed class GraphMerger(IDocumentFormat format)
{
    private const string DataInputMarker = "/in.data.";
    private const string VisibilitySuffix = ".visibility";
    private const string GraphSuffix = ".graph";
    private static readonly GraphDocument EmptyGraph = new([], null, null);
    private static readonly ClassDocument Shell = new(
        1, null, "Shell", MemberVisibility.Public, ClassModifiers.None, null, EmptyGraph, null, null, null, null, null);

    /// <summary>Merges <paramref name="ours"/> and <paramref name="theirs"/> against <paramref name="baseDocument"/>.</summary>
    /// <param name="baseDocument">The common ancestor.</param>
    /// <param name="ours">The current branch's version.</param>
    /// <param name="theirs">The other branch's version.</param>
    /// <param name="cancellationToken">Cancels the merge.</param>
    /// <returns>The merged document, or the conflicts found.</returns>
    public async Task<MergeOutcome> MergeAsync(ClassDocument baseDocument, ClassDocument ours, ClassDocument theirs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseDocument);
        ArgumentNullException.ThrowIfNull(ours);
        ArgumentNullException.ThrowIfNull(theirs);
        var session = new Session(format, cancellationToken);
        ClassDocument merged = await session.MergeClassAsync(baseDocument, ours, theirs).ConfigureAwait(false);
        session.Validate(merged);
        return session.Conflicts.Count == 0 ? new MergeOutcome.Clean(merged) : new MergeOutcome.Conflicted(session.Conflicts);
    }

    private sealed class Session(IDocumentFormat format, CancellationToken cancellationToken)
    {
        public List<MergeConflict> Conflicts { get; } = [];

        public async Task<ClassDocument> MergeClassAsync(ClassDocument b, ClassDocument o, ClassDocument t)
        {
            int schema = Three(b.SchemaVersion, o.SchemaVersion, t.SchemaVersion, "schemaVersion");
            string? ns = Three(b.Namespace, o.Namespace, t.Namespace, "namespace");
            string name = Three(b.Name, o.Name, t.Name, "name");
            MemberVisibility visibility = Three(b.Visibility, o.Visibility, t.Visibility, "visibility");
            ClassModifiers modifiers = Three(b.Modifiers, o.Modifiers, t.Modifiers, "modifiers");
            IReadOnlyList<string>? generics = Three(b.GenericArguments, o.GenericArguments, t.GenericArguments, "genericArguments", SameStrings);
            GraphDocument classGraph = await MergeGraphAsync(b.ClassGraph, o.ClassGraph, t.ClassGraph, "classGraph").ConfigureAwait(false);

            IReadOnlyList<VariableDocument>? variables = await MergeMembersAsync(
                b.Variables, o.Variables, t.Variables, "variables", v => v.Id, PrintAsync, MergeVariableAsync, MergeConflictKind.DuplicateMember).ConfigureAwait(false);
            IReadOnlyList<MethodDocument>? methods = await MergeMembersAsync(
                b.Methods, o.Methods, t.Methods, "methods", m => m.Id, PrintAsync, MergeMethodAsync, MergeConflictKind.DuplicateMember).ConfigureAwait(false);
            IReadOnlyList<ConstructorDocument>? constructors = await MergeMembersAsync(
                b.Constructors, o.Constructors, t.Constructors, "constructors", c => c.Id, PrintAsync, MergeConstructorAsync, MergeConflictKind.DuplicateMember).ConfigureAwait(false);
            IReadOnlyList<EventGraphDocument>? eventGraphs = await MergeMembersAsync(
                b.EventGraphs, o.EventGraphs, t.EventGraphs, "eventGraphs", e => e.Id, PrintAsync, MergeEventGraphAsync, MergeConflictKind.DuplicateMember).ConfigureAwait(false);

            var merged = new ClassDocument(schema, ns, name, visibility, modifiers, generics, classGraph, variables, methods, constructors, eventGraphs, null);
            return merged with { Layout = MergeLayout(b.Layout, o.Layout, t.Layout, GraphNodeIds(merged)) };
        }

        public void Validate(ClassDocument merged)
        {
            foreach ((string path, GraphDocument graph) in Graphs(merged))
            {
                var nodes = graph.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
                foreach (ConnectionDocument connection in graph.Connections ?? [])
                {
                    if (!nodes.Contains(NodeOf(connection.From)) || !nodes.Contains(NodeOf(connection.To)))
                    {
                        Conflicts.Add(new MergeConflict(MergeConflictKind.DanglingConnection, $"{path}.connections[{connection.From} -> {connection.To}]"));
                    }
                }

                foreach (IGrouping<string, ConnectionDocument> input in (graph.Connections ?? [])
                    .Where(connection => connection.To.Contains(DataInputMarker, StringComparison.Ordinal))
                    .GroupBy(connection => connection.To, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1))
                {
                    int slash = input.Key.IndexOf('/', StringComparison.Ordinal);
                    Conflicts.Add(new MergeConflict(MergeConflictKind.DataInputTwice, $"{path}.nodes[{input.Key[..slash]}].pins[{input.Key[(slash + 1)..]}]"));
                }
            }

            ReportDuplicates("variables", (merged.Variables ?? []).Select(v => (v.Id, (string?)v.Name)));
            ReportDuplicates("methods", (merged.Methods ?? []).Select(m => (m.Id, (string?)m.Name)));
            ReportDuplicates("constructors", (merged.Constructors ?? []).Select(c => (c.Id, (string?)null)));
            ReportDuplicates("eventGraphs", (merged.EventGraphs ?? []).Select(e => (e.Id, (string?)e.Name)));
            foreach (IGrouping<string, string> group in AllMemberIds(merged).GroupBy(id => id, StringComparer.Ordinal).Where(group => group.Count() > 1))
            {
                if (!Conflicts.Any(conflict => conflict.Kind == MergeConflictKind.DuplicateMember && conflict.Path.EndsWith($"[{group.Key}]", StringComparison.Ordinal)))
                {
                    Conflicts.Add(new MergeConflict(MergeConflictKind.DuplicateMember, $"members[{group.Key}]"));
                }
            }
        }

        private static IEnumerable<string> AllMemberIds(ClassDocument document) =>
            (document.Variables ?? []).Select(v => v.Id)
                .Concat((document.Methods ?? []).Select(m => m.Id))
                .Concat((document.Constructors ?? []).Select(c => c.Id))
                .Concat((document.EventGraphs ?? []).Select(e => e.Id));

        private void ReportDuplicates(string collection, IEnumerable<(string Id, string? Name)> members)
        {
            (string Id, string? Name)[] list = [.. members];
            foreach (IGrouping<string, (string Id, string? Name)> group in list.GroupBy(member => member.Id, StringComparer.Ordinal).Where(group => group.Count() > 1))
            {
                Conflicts.Add(new MergeConflict(MergeConflictKind.DuplicateMember, $"{collection}[{group.Key}]"));
            }

            foreach (IGrouping<string?, (string Id, string? Name)> group in list.Where(member => member.Name is not null)
                .GroupBy(member => member.Name, StringComparer.Ordinal).Where(group => group.Count() > 1))
            {
                Conflicts.Add(new MergeConflict(MergeConflictKind.DuplicateMember, $"{collection}[{group.Key}]"));
            }
        }

        private static string NodeOf(string endpoint)
        {
            int slash = endpoint.IndexOf('/', StringComparison.Ordinal);
            return slash < 0 ? endpoint : endpoint[..slash];
        }

        private static IEnumerable<(string Path, GraphDocument Graph)> Graphs(ClassDocument document)
        {
            yield return ("classGraph", document.ClassGraph);
            foreach (VariableDocument variable in document.Variables ?? [])
            {
                yield return ($"variables[{variable.Id}].typeGraph", variable.TypeGraph);
                if (variable.Getter is not null)
                {
                    yield return ($"variables[{variable.Id}].getter", variable.Getter.Graph);
                }

                if (variable.Setter is not null)
                {
                    yield return ($"variables[{variable.Id}].setter", variable.Setter.Graph);
                }
            }

            foreach (MethodDocument method in document.Methods ?? [])
            {
                yield return ($"methods[{method.Id}].graph", method.Graph);
            }

            foreach (ConstructorDocument constructor in document.Constructors ?? [])
            {
                yield return ($"constructors[{constructor.Id}].graph", constructor.Graph);
            }

            foreach (EventGraphDocument eventGraph in document.EventGraphs ?? [])
            {
                yield return ($"eventGraphs[{eventGraph.Id}].graph", eventGraph.Graph);
            }
        }

        private static Dictionary<string, HashSet<string>> GraphNodeIds(ClassDocument document)
        {
            var keys = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["class"] = NodeIds(document.ClassGraph),
            };
            foreach (VariableDocument variable in document.Variables ?? [])
            {
                keys[variable.Id + "/type"] = NodeIds(variable.TypeGraph);
                if (variable.Getter is not null)
                {
                    keys[variable.Id + "/get"] = NodeIds(variable.Getter.Graph);
                }

                if (variable.Setter is not null)
                {
                    keys[variable.Id + "/set"] = NodeIds(variable.Setter.Graph);
                }
            }

            foreach (MethodDocument method in document.Methods ?? [])
            {
                keys[method.Id] = NodeIds(method.Graph);
            }

            foreach (ConstructorDocument constructor in document.Constructors ?? [])
            {
                keys[constructor.Id] = NodeIds(constructor.Graph);
            }

            foreach (EventGraphDocument eventGraph in document.EventGraphs ?? [])
            {
                keys[eventGraph.Id] = NodeIds(eventGraph.Graph);
            }

            return keys;
        }

        private static HashSet<string> NodeIds(GraphDocument graph) => graph.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);

        private static bool SameStrings(IReadOnlyList<string>? left, IReadOnlyList<string>? right) =>
            (left ?? []).SequenceEqual(right ?? [], StringComparer.Ordinal);

        private static bool Same<T>(T left, T right) => EqualityComparer<T>.Default.Equals(left, right);

        private T Three<T>(T b, T o, T t, string path, Func<T, T, bool>? same = null, MergeConflictKind kind = MergeConflictKind.Scalar)
        {
            Func<T, T, bool> equal = same ?? Same;
            if (equal(o, t) || equal(b, t))
            {
                return o;
            }

            if (equal(b, o))
            {
                return t;
            }

            Conflicts.Add(new MergeConflict(kind, path));
            return o;
        }

        private async ValueTask<string> PrintAsync(ClassDocument document)
        {
            using var stream = new MemoryStream();
            await format.WriteClassAsync(document, stream, cancellationToken).ConfigureAwait(false);
            return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
        }

        private ValueTask<string> PrintAsync(NodeDocument node) => PrintAsync(Shell with { ClassGraph = new GraphDocument([node], null, null) });

        private ValueTask<string> PrintAsync(LocalVariableDocument local) => PrintAsync(Shell with { ClassGraph = new GraphDocument([], null, [local]) });

        private ValueTask<string> PrintAsync(VariableDocument variable) => PrintAsync(Shell with { Variables = [variable] });

        private ValueTask<string> PrintAsync(MethodDocument method) => PrintAsync(Shell with { Methods = [method] });

        private ValueTask<string> PrintAsync(ConstructorDocument constructor) => PrintAsync(Shell with { Constructors = [constructor] });

        private ValueTask<string> PrintAsync(EventGraphDocument eventGraph) => PrintAsync(Shell with { EventGraphs = [eventGraph] });

        private ValueTask<string> PrintAsync(AccessorDocument accessor) =>
            PrintAsync(new VariableDocument("m0000000000000", "Shell", MemberVisibility.Public, VariableModifiers.None, EmptyGraph, accessor, null));

        private async Task<IReadOnlyList<T>?> MergeMembersAsync<T>(
            IReadOnlyList<T>? b,
            IReadOnlyList<T>? o,
            IReadOnlyList<T>? t,
            string collection,
            Func<T, string> idOf,
            Func<T, ValueTask<string>> printAsync,
            Func<T, T, T, string, Task<T>> mergeBoth,
            MergeConflictKind addAddKind)
            where T : class
        {
            Dictionary<string, T> baseItems = Index(b, idOf);
            Dictionary<string, T> ourItems = Index(o, idOf);
            Dictionary<string, T> theirItems = Index(t, idOf);
            var order = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in (b ?? []).Select(idOf).Concat((o ?? []).Select(idOf)).Concat((t ?? []).Select(idOf)))
            {
                if (seen.Add(id))
                {
                    order.Add(id);
                }
            }

            var result = new List<T>();
            foreach (string id in order)
            {
                string path = $"{collection}[{id}]";
                baseItems.TryGetValue(id, out T? baseItem);
                ourItems.TryGetValue(id, out T? ourItem);
                theirItems.TryGetValue(id, out T? theirItem);
                T? kept;
                if (baseItem is null)
                {
                    kept = ourItem is not null && theirItem is not null
                        ? await KeepAddedAsync(ourItem, theirItem, path, printAsync, addAddKind).ConfigureAwait(false)
                        : ourItem ?? theirItem;
                }
                else if (ourItem is not null && theirItem is not null)
                {
                    kept = await KeepChangedAsync(baseItem, ourItem, theirItem, path, printAsync, mergeBoth).ConfigureAwait(false);
                }
                else
                {
                    kept = await KeepSurvivorAsync(baseItem, ourItem ?? theirItem, path, printAsync).ConfigureAwait(false);
                }

                if (kept is not null)
                {
                    result.Add(kept);
                }
            }

            return result.Count == 0 ? null : result;
        }

        private async Task<T> KeepAddedAsync<T>(T ours, T theirs, string path, Func<T, ValueTask<string>> printAsync, MergeConflictKind kind)
        {
            if (await printAsync(ours).ConfigureAwait(false) != await printAsync(theirs).ConfigureAwait(false))
            {
                Conflicts.Add(new MergeConflict(kind, path));
            }

            return ours;
        }

        private static async Task<T> KeepChangedAsync<T>(
            T baseItem, T ours, T theirs, string path, Func<T, ValueTask<string>> printAsync, Func<T, T, T, string, Task<T>> mergeBoth)
        {
            string printBase = await printAsync(baseItem).ConfigureAwait(false);
            string printOurs = await printAsync(ours).ConfigureAwait(false);
            string printTheirs = await printAsync(theirs).ConfigureAwait(false);
            if (printOurs == printTheirs || printTheirs == printBase)
            {
                return ours;
            }

            return printOurs == printBase ? theirs : await mergeBoth(baseItem, ours, theirs, path).ConfigureAwait(false);
        }

        private async Task<T?> KeepSurvivorAsync<T>(T baseItem, T? survivor, string path, Func<T, ValueTask<string>> printAsync)
            where T : class
        {
            if (survivor is not null && await printAsync(survivor).ConfigureAwait(false) != await printAsync(baseItem).ConfigureAwait(false))
            {
                Conflicts.Add(new MergeConflict(MergeConflictKind.DeleteModify, path));
            }

            return null;
        }

        private static Dictionary<string, T> Index<T>(IReadOnlyList<T>? items, Func<T, string> idOf)
        {
            var index = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (T item in items ?? [])
            {
                index.TryAdd(idOf(item), item);
            }

            return index;
        }

        private async Task<GraphDocument> MergeGraphAsync(GraphDocument b, GraphDocument o, GraphDocument t, string path)
        {
            IReadOnlyList<NodeDocument>? nodes = await MergeMembersAsync(
                b.Nodes, o.Nodes, t.Nodes, path + ".nodes", node => node.Id, PrintAsync, MergeNodeAsync, MergeConflictKind.NodeProperty).ConfigureAwait(false);
            IReadOnlyList<LocalVariableDocument>? locals = await MergeMembersAsync(
                b.Locals, o.Locals, t.Locals, path + ".locals", local => local.Name, PrintAsync,
                (_, ours, _, localPath) =>
                {
                    Conflicts.Add(new MergeConflict(MergeConflictKind.Scalar, localPath));
                    return Task.FromResult(ours);
                },
                MergeConflictKind.Scalar).ConfigureAwait(false);

            var baseConnections = (b.Connections ?? []).ToHashSet();
            var ourConnections = (o.Connections ?? []).ToHashSet();
            var theirConnections = (t.Connections ?? []).ToHashSet();
            IEnumerable<ConnectionDocument> connections = baseConnections
                .Concat(ourConnections.Except(baseConnections))
                .Concat(theirConnections.Except(baseConnections))
                .Distinct()
                .Where(connection => !(baseConnections.Contains(connection) && (!ourConnections.Contains(connection) || !theirConnections.Contains(connection))));
            ConnectionDocument[] sorted = [.. connections.OrderBy(c => c.From, StringComparer.Ordinal).ThenBy(c => c.To, StringComparer.Ordinal)];
            return new GraphDocument(nodes ?? [], sorted.Length == 0 ? null : sorted, locals);
        }

        private async Task<NodeDocument> MergeNodeAsync(NodeDocument b, NodeDocument o, NodeDocument t, string path)
        {
            string baseProperties = await PrintAsync(b with { Pins = null }).ConfigureAwait(false);
            string ourProperties = await PrintAsync(o with { Pins = null }).ConfigureAwait(false);
            string theirProperties = await PrintAsync(t with { Pins = null }).ConfigureAwait(false);
            NodeDocument chosen = o;
            if (ourProperties != theirProperties && theirProperties != baseProperties)
            {
                if (ourProperties == baseProperties)
                {
                    chosen = t;
                }
                else
                {
                    Conflicts.Add(new MergeConflict(MergeConflictKind.NodeProperty, path));
                }
            }

            return chosen with { Pins = MergePins(b.Pins, o.Pins, t.Pins, path) };
        }

        private IReadOnlyList<PinStateDocument>? MergePins(
            IReadOnlyList<PinStateDocument>? b, IReadOnlyList<PinStateDocument>? o, IReadOnlyList<PinStateDocument>? t, string path)
        {
            Dictionary<string, PinStateDocument> baseItems = Index(b, pin => pin.Pin);
            Dictionary<string, PinStateDocument> ourItems = Index(o, pin => pin.Pin);
            Dictionary<string, PinStateDocument> theirItems = Index(t, pin => pin.Pin);
            var result = new List<PinStateDocument>();
            foreach (string key in (b ?? []).Concat(o ?? []).Concat(t ?? []).Select(pin => pin.Pin).Distinct(StringComparer.Ordinal))
            {
                baseItems.TryGetValue(key, out PinStateDocument? baseItem);
                ourItems.TryGetValue(key, out PinStateDocument? ourItem);
                theirItems.TryGetValue(key, out PinStateDocument? theirItem);
                PinStateDocument? kept = Three(baseItem, ourItem, theirItem, $"{path}.pins[{key}]", kind: MergeConflictKind.PinValue);
                if (kept is not null)
                {
                    result.Add(kept);
                }
            }

            return result.Count == 0 ? null : result;
        }

        private async Task<MethodDocument> MergeMethodAsync(MethodDocument b, MethodDocument o, MethodDocument t, string path) =>
            o with
            {
                Name = Three(b.Name, o.Name, t.Name, path + ".name"),
                Visibility = Three(b.Visibility, o.Visibility, t.Visibility, path + VisibilitySuffix),
                Modifiers = Three(b.Modifiers, o.Modifiers, t.Modifiers, path + ".modifiers"),
                Graph = await MergeGraphAsync(b.Graph, o.Graph, t.Graph, path + GraphSuffix).ConfigureAwait(false),
            };

        private async Task<ConstructorDocument> MergeConstructorAsync(ConstructorDocument b, ConstructorDocument o, ConstructorDocument t, string path) =>
            o with
            {
                Visibility = Three(b.Visibility, o.Visibility, t.Visibility, path + VisibilitySuffix),
                Graph = await MergeGraphAsync(b.Graph, o.Graph, t.Graph, path + GraphSuffix).ConfigureAwait(false),
            };

        private async Task<EventGraphDocument> MergeEventGraphAsync(EventGraphDocument b, EventGraphDocument o, EventGraphDocument t, string path) =>
            o with
            {
                Name = Three(b.Name, o.Name, t.Name, path + ".name"),
                Graph = await MergeGraphAsync(b.Graph, o.Graph, t.Graph, path + GraphSuffix).ConfigureAwait(false),
            };

        private async Task<VariableDocument> MergeVariableAsync(VariableDocument b, VariableDocument o, VariableDocument t, string path) =>
            o with
            {
                Name = Three(b.Name, o.Name, t.Name, path + ".name"),
                Visibility = Three(b.Visibility, o.Visibility, t.Visibility, path + VisibilitySuffix),
                Modifiers = Three(b.Modifiers, o.Modifiers, t.Modifiers, path + ".modifiers"),
                TypeGraph = await MergeGraphAsync(b.TypeGraph, o.TypeGraph, t.TypeGraph, path + ".typeGraph").ConfigureAwait(false),
                Getter = await MergeAccessorAsync(b.Getter, o.Getter, t.Getter, path + ".getter").ConfigureAwait(false),
                Setter = await MergeAccessorAsync(b.Setter, o.Setter, t.Setter, path + ".setter").ConfigureAwait(false),
            };

        private async Task<AccessorDocument?> MergeAccessorAsync(AccessorDocument? b, AccessorDocument? o, AccessorDocument? t, string path)
        {
            if (o is null && t is null)
            {
                return null;
            }

            if (o is null || t is null)
            {
                AccessorDocument present = o ?? t ?? throw new InvalidOperationException("One accessor is present.");
                if (b is null)
                {
                    return present;
                }

                if (await PrintAsync(present).ConfigureAwait(false) != await PrintAsync(b).ConfigureAwait(false))
                {
                    Conflicts.Add(new MergeConflict(MergeConflictKind.DeleteModify, path));
                    return present;
                }

                return null;
            }

            if (b is null)
            {
                if (await PrintAsync(o).ConfigureAwait(false) != await PrintAsync(t).ConfigureAwait(false))
                {
                    Conflicts.Add(new MergeConflict(MergeConflictKind.Scalar, path));
                }

                return o;
            }

            return o with
            {
                Visibility = Three(b.Visibility, o.Visibility, t.Visibility, path + VisibilitySuffix),
                Graph = await MergeGraphAsync(b.Graph, o.Graph, t.Graph, path + GraphSuffix).ConfigureAwait(false),
            };
        }

        private static SortedDictionary<string, SortedDictionary<string, int[]>>? MergeLayout(
            SortedDictionary<string, SortedDictionary<string, int[]>>? b,
            SortedDictionary<string, SortedDictionary<string, int[]>>? o,
            SortedDictionary<string, SortedDictionary<string, int[]>>? t,
            Dictionary<string, HashSet<string>> known)
        {
            var result = new SortedDictionary<string, SortedDictionary<string, int[]>>(StringComparer.Ordinal);
            foreach (string graphKey in Keys(o).Concat(Keys(t)).Distinct(StringComparer.Ordinal))
            {
                if (!known.TryGetValue(graphKey, out HashSet<string>? nodes))
                {
                    continue;
                }

                SortedDictionary<string, int[]>? baseGraph = Find(b, graphKey);
                SortedDictionary<string, int[]>? ourGraph = Find(o, graphKey);
                SortedDictionary<string, int[]>? theirGraph = Find(t, graphKey);
                var positions = new SortedDictionary<string, int[]>(StringComparer.Ordinal);
                foreach (string nodeId in Keys(ourGraph).Concat(Keys(theirGraph)).Distinct(StringComparer.Ordinal).Where(nodes.Contains))
                {
                    int[]? baseValue = Find(baseGraph, nodeId);
                    int[]? ourValue = Find(ourGraph, nodeId);
                    int[]? theirValue = Find(theirGraph, nodeId);
                    int[]? value = SamePosition(ourValue, baseValue) ? theirValue : ourValue;
                    if (value is not null)
                    {
                        positions[nodeId] = value;
                    }
                }

                if (positions.Count > 0)
                {
                    result[graphKey] = positions;
                }
            }

            return result.Count == 0 ? null : result;
        }

        private static IEnumerable<string> Keys<TValue>(SortedDictionary<string, TValue>? map) => map is null ? [] : map.Keys;

        private static bool SamePosition(int[]? left, int[]? right) =>
            left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);

        private static TValue? Find<TValue>(SortedDictionary<string, TValue>? map, string key)
            where TValue : class =>
            map is not null && map.TryGetValue(key, out TValue? value) ? value : null;
    }
}
