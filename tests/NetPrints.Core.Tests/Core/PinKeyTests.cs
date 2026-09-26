using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Tests.Characterization;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Core
{
    /// <summary>DF-T19, model part: <see cref="Node.GetPinKeyName"/> and <see cref="PinKeys"/>.</summary>
    public class PinKeyTests
    {
        private static IEnumerable<NodePin> AllPinsOf(Node node)
        {
            foreach (NodeInputDataPin pin in node.InputDataPins)
            {
                yield return pin;
            }

            foreach (NodeOutputDataPin pin in node.OutputDataPins)
            {
                yield return pin;
            }

            foreach (NodeInputExecPin pin in node.InputExecPins)
            {
                yield return pin;
            }

            foreach (NodeOutputExecPin pin in node.OutputExecPins)
            {
                yield return pin;
            }

            foreach (NodeInputTypePin pin in node.InputTypePins)
            {
                yield return pin;
            }

            foreach (NodeOutputTypePin pin in node.OutputTypePins)
            {
                yield return pin;
            }
        }

        private static IEnumerable<NodeGraph> AllGraphsOf(ClassGraph cls)
        {
            yield return cls;

            foreach (Variable variable in cls.Variables)
            {
                yield return variable.TypeGraph;

                if (variable.GetterMethod is not null)
                {
                    yield return variable.GetterMethod;
                }

                if (variable.SetterMethod is not null)
                {
                    yield return variable.SetterMethod;
                }
            }

            foreach (var method in cls.Methods)
            {
                yield return method;
            }

            foreach (var constructor in cls.Constructors)
            {
                yield return constructor;
            }
        }

        [Fact]
        public void PinReferencesMatchGoldenFileAndRoundTripThroughFind()
        {
            string projectPath = Path.Combine(Path.GetTempPath(), "netprints-pinkeys-" + Guid.NewGuid().ToString("N"), "AllNodes.netpp");

            Project project;
            using (IdGeneration.Use(new SeededIdGenerator(42)))
            {
                project = AllNodesFixtureFactory.CreateAllNodes(projectPath);
            }

            ClassGraph cls = project.Classes.Single();
            var lines = new List<string>();

            foreach (NodeGraph graph in AllGraphsOf(cls))
            {
                string graphKey = GraphKeys.For(graph);

                foreach (Node node in graph.Nodes)
                {
                    var seenRefs = new HashSet<string>(StringComparer.Ordinal);

                    foreach (NodePin pin in AllPinsOf(node))
                    {
                        string pinRef = PinKeys.For(pin);

                        Assert.True(seenRefs.Add(pinRef), $"Duplicate pin reference '{pinRef}' on node {node.Id}.");
                        Assert.Same(pin, PinKeys.Find(node, pinRef));

                        lines.Add($"{cls.FullName}/{graphKey}/{node.Id}: {pinRef}");
                    }
                }
            }

            lines.Sort(StringComparer.Ordinal);

            string goldenPath = Path.Combine(SampleProjectFactory.FindRepositoryRoot(),
                "tests", "NetPrints.Core.Tests", "Fixtures", "Golden", "PinKeys.golden.txt");

            if (Environment.GetEnvironmentVariable(GoldenCSharpTests.UpdateSnapshotsVariable) == "1")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
                File.WriteAllLines(goldenPath, lines);
            }

            Assert.True(File.Exists(goldenPath), $"Missing {goldenPath}; regenerate with {GoldenCSharpTests.UpdateSnapshotsVariable}=1");
            string[] golden = File.ReadAllLines(goldenPath);
            Assert.Equal(golden, lines);
        }

        [Fact]
        public void TwoInt32ReturnValuesDisambiguateWithTilde2()
        {
            var specifier = new MethodSpecifier("Foo", Array.Empty<MethodParameter>(),
                new BaseType[] { TypeSpecifier.FromType<int>(), TypeSpecifier.FromType<int>() },
                MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType(typeof(Console)), Array.Empty<BaseType>());

            var method = new MethodGraph("M");
            var call = new CallMethodNode(method, specifier);

            Assert.Equal("out.data.Int32", PinKeys.For(call.OutputDataPins[0]));
            Assert.Equal("out.data.Int32~2", PinKeys.For(call.OutputDataPins[1]));
        }

        [Fact]
        public void RenamingAnArgumentPinKeepsItsPositionalKey()
        {
            var method = new MethodGraph("M");
            var entry = (MethodEntryNode)method.EntryNode;
            entry.AddArgument();

            NodeOutputDataPin argumentPin = entry.OutputDataPins[0];
            Assert.Equal("out.data.Input0", PinKeys.For(argumentPin));

            argumentPin.Name = "renamed";

            Assert.Equal("out.data.Input0", PinKeys.For(argumentPin));
        }

        [Theory]
        [InlineData("")]
        [InlineData("in.data")]
        [InlineData("x.data.a")]
        [InlineData("in.data.0")]
        public void FindReturnsNullForMalformedOrIndexFormReferences(string reference)
        {
            var method = new MethodGraph("M");

            Assert.Null(PinKeys.Find(method.EntryNode, reference));
        }
    }
}
