using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary>
    /// DF-T23: a plain <c>git merge-file</c> three-way merge of two branches that each add an
    /// unrelated node pair to the same method graph (document-format.md §2.6, research R17 §5.4).
    /// </summary>
    public class MergeTests
    {
        private static readonly NodeDocumentConverterRegistry Registry = new(NodeDocumentConverterRegistry.BuiltIn, []);

        private static (Project Project, ClassGraph Class, MethodGraph Method) BuildBase()
        {
            Project project = Project.CreateNew("M", "M");
            var cls = new ClassGraph { Name = "C", Namespace = "M", Visibility = MemberVisibility.Public, Project = project };
            project.Classes.Add(cls);
            SetNodeId(cls, cls.ReturnNode, "n0000000class");

            var method = new MethodGraph("Main") { Class = cls, Visibility = MemberVisibility.Public };
            method.Id = "mbase0000000";
            cls.Methods.Add(method);

            Node entry = method.EntryNode;
            Node ret = method.MainReturnNode;
            var call1 = new CallMethodNode(method, ArbitraryCall("Call1"));
            var call2 = new CallMethodNode(method, ArbitraryCall("Call2"));

            SetNodeId(method, entry, "nb00000");
            SetNodeId(method, call1, "nk00000");
            SetNodeId(method, call2, "nr00000");
            SetNodeId(method, ret, "nz00000");

            Position(entry, 0);
            Position(call1, 1);
            Position(call2, 2);
            Position(ret, 3);

            GraphUtil.ConnectExecPins(((MethodEntryNode)entry).InitialExecutionPin, call1.InputExecPins[0]);
            GraphUtil.ConnectExecPins(call1.OutputExecPins[0], call2.InputExecPins[0]);
            GraphUtil.ConnectExecPins(call2.OutputExecPins[0], ((ReturnNode)ret).ReturnPin);

            return (project, cls, method);
        }

        /// <summary>Adds a call fed by a connected literal (the T042 regression: a literal's connection
        /// must survive to JSON) — branch A calls <c>Console.WriteLine(string)</c>, branch B
        /// <c>Console.Beep()</c>, so the two branches' additions share no text at all and git's
        /// line-based merge cannot align them against each other.</summary>
        private static void AddLiteralAndCall(MethodGraph method, string literalId, string callId, int column, bool useStringOverload)
        {
            CallMethodNode call;
            if (useStringOverload)
            {
                LiteralNode literal = LiteralNode.WithValue(method, "text");
                SetNodeId(method, literal, literalId);
                Position(literal, column);

                var writeLine = new MethodSpecifier("WriteLine",
                    [new MethodParameter("value", TypeSpecifier.FromType<string>(), MethodParameterPassType.Default, false, null)],
                    [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType(typeof(Console)), []);
                call = new CallMethodNode(method, writeLine);
                Position(call, column + 1);
                GraphUtil.ConnectDataPins(literal.ValuePin, call.ArgumentPins.Single());
            }
            else
            {
                LiteralNode literal = LiteralNode.WithValue(method, 7);
                SetNodeId(method, literal, literalId);
                Position(literal, column);

                var beep = new MethodSpecifier("Beep",
                    [new MethodParameter("frequency", TypeSpecifier.FromType<int>(), MethodParameterPassType.Default, false, null)],
                    [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType(typeof(Console)), []);
                call = new CallMethodNode(method, beep);
                Position(call, column + 1);
                GraphUtil.ConnectDataPins(literal.ValuePin, call.ArgumentPins.Single());
            }

            SetNodeId(method, call, callId);
        }

        private static MethodSpecifier ArbitraryCall(string name) =>
            new(name, [], [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType(typeof(Console)), []);

        private static void SetNodeId(NodeGraph graph, Node node, string id)
        {
            string previous = node.Id;
            node.Id = id;
            graph.ReindexNode(node, previous);
        }

        private static void Position(Node node, int column)
        {
            node.PositionX = column * 120;
            node.PositionY = 0;
        }

        private static async Task<byte[]> ToJsonBytesAsync(ClassGraph cls)
        {
            var mapper = new DocumentMapper(Registry);
            ClassDocument document = mapper.ToDocument(cls);
            var format = new JsonDocumentFormat(new NetPrintsJsonOptions(Registry), new DocumentMigrator([]));
            using var stream = new MemoryStream();
            await format.WriteClassAsync(document, stream, TestContext.Current.CancellationToken);
            return stream.ToArray();
        }

        private static bool GitIsOnPath()
        {
            try
            {
                using var probe = Process.Start(new ProcessStartInfo("git", "--version")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                });
                probe?.WaitForExit();
                return probe is not null;
            }
            catch (Win32Exception)
            {
                return false;
            }
        }

        private static async Task<(int ExitCode, string StdOut)> RunGitMergeFileAsync(string currentPath, string basePath, string otherPath)
        {
            var startInfo = new ProcessStartInfo("git")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("merge-file");
            startInfo.ArgumentList.Add("-p");
            startInfo.ArgumentList.Add(currentPath);
            startInfo.ArgumentList.Add(basePath);
            startInfo.ArgumentList.Add(otherPath);

            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("git did not start.");
            string stdOut = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            return (process.ExitCode, stdOut);
        }

        [Fact]
        public async Task TwoBranchesAddingUnrelatedNodesMergeWithOneConflictAtTheNodesTail()
        {
            if (!GitIsOnPath())
            {
                Assert.Skip("git is not on PATH.");
                return;
            }

            (_, ClassGraph baseCls, _) = BuildBase();
            byte[] baseBytes = await ToJsonBytesAsync(baseCls);

            (_, ClassGraph aCls, MethodGraph aMethod) = BuildBase();
            AddLiteralAndCall(aMethod, "nd00000", "nf00000", 10, useStringOverload: true);
            byte[] currentBytes = await ToJsonBytesAsync(aCls);

            (_, ClassGraph bCls, MethodGraph bMethod) = BuildBase();
            AddLiteralAndCall(bMethod, "nm00000", "np00000", 20, useStringOverload: false);
            byte[] otherBytes = await ToJsonBytesAsync(bCls);

            string tempDir = Directory.CreateTempSubdirectory("netprints-merge-").FullName;
            try
            {
                string currentPath = Path.Combine(tempDir, "current.netpc.json");
                string basePath = Path.Combine(tempDir, "base.netpc.json");
                string otherPath = Path.Combine(tempDir, "other.netpc.json");
                await File.WriteAllBytesAsync(currentPath, currentBytes, TestContext.Current.CancellationToken);
                await File.WriteAllBytesAsync(basePath, baseBytes, TestContext.Current.CancellationToken);
                await File.WriteAllBytesAsync(otherPath, otherBytes, TestContext.Current.CancellationToken);

                (int exitCode, string merged) = await RunGitMergeFileAsync(currentPath, basePath, otherPath);

                Assert.Equal(1, exitCode);
                Assert.Equal(1, CountOccurrences(merged, "<<<<<<<"));
                Assert.Equal(1, CountOccurrences(merged, "======="));
                Assert.Equal(1, CountOccurrences(merged, ">>>>>>>"));

                int conflictStart = merged.IndexOf("<<<<<<<", StringComparison.Ordinal);
                int connectionsStart = merged.IndexOf("\"connections\"", StringComparison.Ordinal);
                Assert.True(conflictStart < connectionsStart, "The conflict must be inside 'nodes', before 'connections' starts.");
                Assert.Contains("nd00000", merged);
                Assert.Contains("nf00000", merged);
                Assert.Contains("nm00000", merged);
                Assert.Contains("np00000", merged);

                // Connections and layout came from disjoint, already-sorted gaps (document-format.md
                // §2.3.1): both branches' additions are present, outside the single conflict hunk.
                string outsideConflict = string.Concat(merged.AsSpan(0, conflictStart), merged.AsSpan(merged.LastIndexOf(">>>>>>>", StringComparison.Ordinal)));
                Assert.DoesNotContain("<<<<<<<", outsideConflict);

                // A human resolving the hunk keeps both sides' new node/connection/layout entries
                // (document-format.md §2.6's documented limit: git's line-based merge cannot combine
                // the two additions itself, since it never inserted the missing ',' + duplicated
                // closing lines the second node needs). Reconstructed here from the two pre-conflict
                // documents rather than by patching git's raw text, which would need brace-depth
                // tracking beyond plain line splicing to duplicate that shared closing tail correctly.
                string resolved = MergeBothSides(currentBytes, otherBytes);
                JsonNode? parsed = JsonNode.Parse(resolved);
                Assert.NotNull(parsed);

                var format = new JsonDocumentFormat(new NetPrintsJsonOptions(Registry), new DocumentMigrator([]));
                using var resolvedStream = new MemoryStream(Encoding.UTF8.GetBytes(resolved));
                ClassDocument resolvedDocument = await format.ReadClassAsync(resolvedStream, new DocumentId("merged.netpc.json"), TestContext.Current.CancellationToken);

                var mapper = new DocumentMapper(Registry);
                var issues = new System.Collections.Generic.List<DocumentIssue>();
                Project mergedProject = Project.CreateNew("M", "M");
                mapper.FromDocument(resolvedDocument, mergedProject, issues, new DocumentId("merged.netpc.json"));
                Assert.Empty(issues);

                System.Collections.Generic.IReadOnlyList<MethodDocument>? methods = resolvedDocument.Methods;
                Assert.NotNull(methods);
                GraphDocument mergedGraph = methods.Single().Graph;
                Assert.Equal(8, mergedGraph.Nodes.Count);
                Assert.Equal(5, mergedGraph.Connections?.Count ?? 0);
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        /// <summary>
        /// Documents the limit git merge-file's line-based diff still has even with sorted, one-line-
        /// per-element inline records (research §5.4): two branches that each insert into the exact
        /// same interior gap of a sorted array still conflict there, even though `nodes`' own single
        /// conflict (above) is avoided by inserting into disjoint gaps.
        /// </summary>
        [Fact]
        public async Task TwoBranchesInsertingIntoTheSameGapStillConflict()
        {
            if (!GitIsOnPath())
            {
                Assert.Skip("git is not on PATH.");
                return;
            }

            (_, ClassGraph baseCls, _) = BuildBase();
            byte[] baseBytes = await ToJsonBytesAsync(baseCls);

            (_, ClassGraph aCls, MethodGraph aMethod) = BuildBase();
            AddLiteralAndCall(aMethod, "ne00001", "ne00002", 10, useStringOverload: true);
            byte[] currentBytes = await ToJsonBytesAsync(aCls);

            (_, ClassGraph bCls, MethodGraph bMethod) = BuildBase();
            AddLiteralAndCall(bMethod, "ne00003", "ne00004", 20, useStringOverload: true);
            byte[] otherBytes = await ToJsonBytesAsync(bCls);

            string tempDir = Directory.CreateTempSubdirectory("netprints-merge-gap-").FullName;
            try
            {
                string currentPath = Path.Combine(tempDir, "current.netpc.json");
                string basePath = Path.Combine(tempDir, "base.netpc.json");
                string otherPath = Path.Combine(tempDir, "other.netpc.json");
                await File.WriteAllBytesAsync(currentPath, currentBytes, TestContext.Current.CancellationToken);
                await File.WriteAllBytesAsync(basePath, baseBytes, TestContext.Current.CancellationToken);
                await File.WriteAllBytesAsync(otherPath, otherBytes, TestContext.Current.CancellationToken);

                (int exitCode, string merged) = await RunGitMergeFileAsync(currentPath, basePath, otherPath);

                Assert.True(exitCode > 0, "Two inserts into the same sorted-array gap are expected to conflict.");
                Assert.True(CountOccurrences(merged, "<<<<<<<") >= 1);
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        private static int CountOccurrences(string text, string token)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }

        /// <summary>Resolves the conflict "by keeping both sides": <paramref name="currentJson"/>'s
        /// single method graph gains every node, connection and layout entry of
        /// <paramref name="otherJson"/>'s same graph that it does not already have (by id, by
        /// <c>from</c>/<c>to</c>, and by node id respectively) — exactly the union a person resolving
        /// the hunk in a text editor would end up typing by hand.</summary>
        private static string MergeBothSides(byte[] currentJson, byte[] otherJson)
        {
            JsonObject currentRoot = RequireObject(JsonNode.Parse(currentJson));
            JsonObject otherRoot = RequireObject(JsonNode.Parse(otherJson));

            JsonObject currentGraph = RequireObject(RequireObject(RequireArray(currentRoot["methods"])[0])["graph"]);
            JsonObject otherGraph = RequireObject(RequireObject(RequireArray(otherRoot["methods"])[0])["graph"]);

            JsonArray currentNodes = RequireArray(currentGraph["nodes"]);
            var currentNodeIds = currentNodes.Select(n => RequireString(RequireObject(n)["id"])).ToHashSet();
            foreach (JsonNode? node in RequireArray(otherGraph["nodes"]))
            {
                Assert.NotNull(node);
                if (currentNodeIds.Add(RequireString(RequireObject(node)["id"])))
                {
                    currentNodes.Add(node.DeepClone());
                }
            }

            // `??=` on an indexer short-circuits the setter when the getter is already non-null (C#
            // null-coalescing assignment is `expr ?? (expr = value)`), so this never re-parents an
            // already-attached JsonNode into its own parent.
            currentGraph["connections"] ??= new JsonArray();
            JsonArray currentConnections = RequireArray(currentGraph["connections"]);
            var currentConnectionKeys = currentConnections.Select(ConnectionKey).ToHashSet();
            foreach (JsonNode? connection in otherGraph["connections"] is JsonNode oc ? RequireArray(oc) : new JsonArray())
            {
                Assert.NotNull(connection);
                if (currentConnectionKeys.Add(ConnectionKey(connection)))
                {
                    currentConnections.Add(connection.DeepClone());
                }
            }

            currentRoot["layout"] ??= new JsonObject();
            JsonObject currentLayout = RequireObject(currentRoot["layout"]);
            foreach ((string graphKey, JsonNode? otherPositions) in otherRoot["layout"] is JsonNode ol ? RequireObject(ol) : new JsonObject())
            {
                currentLayout[graphKey] ??= new JsonObject();
                JsonObject currentPositions = RequireObject(currentLayout[graphKey]);
                foreach ((string nodeId, JsonNode? position) in RequireObject(otherPositions))
                {
                    Assert.NotNull(position);
                    currentPositions.TryAdd(nodeId, position.DeepClone());
                }
            }

            return currentRoot.ToJsonString();

            (string From, string To) ConnectionKey(JsonNode? connection)
            {
                JsonObject obj = RequireObject(connection);
                return (RequireString(obj["from"]), RequireString(obj["to"]));
            }
        }

        private static JsonObject RequireObject(JsonNode? node)
        {
            Assert.NotNull(node);
            return node.AsObject();
        }

        private static JsonArray RequireArray(JsonNode? node)
        {
            Assert.NotNull(node);
            return node.AsArray();
        }

        private static string RequireString(JsonNode? node)
        {
            Assert.NotNull(node);
            return node.GetValue<string>();
        }
    }
}
