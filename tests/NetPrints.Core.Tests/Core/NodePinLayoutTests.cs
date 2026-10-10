using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Tests.Characterization;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Core
{
    /// <summary>
    /// Characterization of every built-in node kind's pin layout: the order of each pin collection, each
    /// pin's reference, initial type and unconnected default, and the collection slot every public
    /// <see cref="NodePin"/> property returns. Index-based code (the typed pin properties, the
    /// per-pin translator exec index, the editor's row pairing) depends on that order.
    /// </summary>
    public class NodePinLayoutTests
    {
        private const string GoldenFileName = "NodePinLayout.golden.txt";

        private const string NoValue = "-";

        private static readonly TypeSpecifier IntType = TypeSpecifier.FromType<int>();

        private static readonly TypeSpecifier StringType = TypeSpecifier.FromType<string>();

        private static readonly MethodParameter StringParameter =
            new("value", StringType, MethodParameterPassType.Default, false, null);

        private sealed record Variant(string Name, Func<Node> Build, string? PurityGroup = null);

        private static MethodSpecifier StaticMethod() =>
            new("WriteLine", [StringParameter], [], MethodModifiers.Static, MemberVisibility.Public,
                TypeSpecifier.FromType(typeof(Console)), []);

        private static MethodSpecifier ReturningStaticMethod() =>
            new("Abs", [new MethodParameter("value", IntType, MethodParameterPassType.Default, false, null)],
                [IntType], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType(typeof(Math)), []);

        private static MethodSpecifier InstanceMethod() =>
            new("Substring", [new MethodParameter("start", IntType, MethodParameterPassType.Default, false, null)],
                [StringType], MethodModifiers.None, MemberVisibility.Public, StringType, []);

        private static VariableSpecifier Variable(string name, VariableModifiers modifiers, bool local = false) =>
            new(name, IntType, MemberVisibility.Public, MemberVisibility.Public,
                local ? null : TypeSpecifier.FromType(typeof(Uri)), modifiers)
            {
                Scope = local ? VariableScope.Local : VariableScope.Member,
            };

        private static List<Variant> BuildVariants()
        {
            var variants = new List<Variant>();

            void Add(string name, Func<Node> build) => variants.Add(new Variant(name, build));

            void AddPurity(string type, string name, Func<NodeGraph, Node> create)
            {
                string group = $"{type}/{name}";

                variants.Add(new Variant(name, () => create(new MethodGraph("M")), group));

                if (!create(new MethodGraph("M")).CanSetPure)
                {
                    return;
                }

                variants.Add(new Variant($"{name}, toggled", () =>
                {
                    Node node = create(new MethodGraph("M"));
                    node.IsPure = !node.IsPure;
                    return node;
                }, group));

                variants.Add(new Variant($"{name}, toggled twice", () =>
                {
                    Node node = create(new MethodGraph("M"));
                    node.IsPure = !node.IsPure;
                    node.IsPure = !node.IsPure;
                    return node;
                }, group));
            }

            Add("default", () => new MethodGraph("M").MethodEntryNode);
            Add("2 arguments + generic", () =>
            {
                MethodEntryNode entry = new MethodGraph("M").MethodEntryNode;
                entry.AddArgument();
                entry.AddArgument();
                entry.AddGenericArgument();
                return entry;
            });
            Add("default", () => new MethodGraph("M").MainReturnNode);
            Add("2 return types", () =>
            {
                ReturnNode ret = new MethodGraph("M").MainReturnNode;
                ret.AddReturnType();
                ret.AddReturnType();
                return ret;
            });
            Add("default", () => new ConstructorGraph().EntryNode);
            Add("default", () => new EventEntryNode(new EventGraph("E"), "E"));
            Add("2 arguments", () =>
            {
                var entry = new EventEntryNode(new EventGraph("E"), "E");
                entry.AddArgument();
                entry.AddArgument();
                return entry;
            });
            Add("default", () => new ClassGraph().ReturnNode);
            Add("1 interface", () =>
            {
                ClassReturnNode ret = new ClassGraph().ReturnNode;
                ret.AddInterfacePin();
                return ret;
            });
            Add("default", () => new TypeGraph().ReturnNode);

            Add("default", () => new IfElseNode(new MethodGraph("M")));
            Add("default", () => new ForLoopNode(new MethodGraph("M")));
            Add("default", () => new ThrowNode(new MethodGraph("M")));
            Add("int", () => new TypeNode(new MethodGraph("M"), IntType));
            Add("default", () => new TypeOfNode(new MethodGraph("M")));
            Add("default", () => new DefaultNode(new MethodGraph("M")));
            Add("default", () => new MakeArrayTypeNode(new MethodGraph("M")));

            AddPurity("ExplicitCastNode", "default", g => new ExplicitCastNode(g));
            AddPurity("TernaryNode", "default", g => new TernaryNode(g));
            AddPurity("AwaitNode", "default", g => new AwaitNode(g));
            AddPurity("CallMethodNode", "static", g => new CallMethodNode(g, ReturningStaticMethod()));
            AddPurity("CallMethodNode", "static void", g => new CallMethodNode(g, StaticMethod()));
            AddPurity("CallMethodNode", "instance", g => new CallMethodNode(g, InstanceMethod()));
            AddPurity("ConstructorNode", "1 parameter", g =>
                new ConstructorNode(g, new ConstructorSpecifier([StringParameter], TypeSpecifier.FromType<Exception>())));

            Add("static, Catch wired", () =>
            {
                var method = new MethodGraph("M");
                var call = new CallMethodNode(method, StaticMethod());
                GraphUtil.ConnectExecPins(
                    call.CatchPin ?? throw new InvalidOperationException("An impure call has a Catch pin."),
                    method.MainReturnNode.ReturnPin);
                return call;
            });

            Add("initializer, no elements", () => new MakeArrayNode(new MethodGraph("M")));
            Add("initializer, 2 elements", () =>
            {
                var node = new MakeArrayNode(new MethodGraph("M"));
                node.AddElementPin();
                node.AddElementPin();
                return node;
            });
            Add("predefined size", () => new MakeArrayNode(new MethodGraph("M")) { UsePredefinedSize = true });
            Add("predefined size then initializer", () =>
            {
                var node = new MakeArrayNode(new MethodGraph("M")) { UsePredefinedSize = true };
                node.UsePredefinedSize = false;
                return node;
            });

            Add("static", () => new MakeDelegateNode(new MethodGraph("M"), StaticMethod()));
            Add("instance", () => new MakeDelegateNode(new MethodGraph("M"), InstanceMethod()));

            Add("int", () => new LiteralNode(new MethodGraph("M"), IntType));
            Add("string", () => new LiteralNode(new MethodGraph("M"), StringType));
            Add("bool", () => new LiteralNode(new MethodGraph("M"), TypeSpecifier.FromType<bool>()));
            Add("decimal", () => new LiteralNode(new MethodGraph("M"), TypeSpecifier.FromType<decimal>()));
            Add("enum", () => new LiteralNode(new MethodGraph("M"), new TypeSpecifier("System.DayOfWeek", isEnum: true)));

            Add("2 executions", () => RerouteNode.MakeExecution(new MethodGraph("M"), 2));
            Add("1 data", () => RerouteNode.MakeData(new MethodGraph("M"), [Tuple.Create<BaseType, BaseType>(IntType, IntType)]));
            Add("1 type", () => RerouteNode.MakeType(new MethodGraph("M"), 1));

            foreach (var (name, spec) in new[]
            {
                ("static", Variable("v", VariableModifiers.Static)),
                ("instance", Variable("v", VariableModifiers.None)),
                ("local", Variable("v", VariableModifiers.None, local: true)),
                ("indexer", Variable("this[]", VariableModifiers.None)),
            })
            {
                Add(name, () => new VariableGetterNode(new MethodGraph("M"), spec));
                Add(name, () => new VariableSetterNode(new MethodGraph("M"), spec));
            }

            return variants;
        }

        private static IEnumerable<(string Collection, List<NodePin> Pins)> CollectionsOf(Node node)
        {
            yield return (nameof(Node.InputDataPins), node.InputDataPins.Cast<NodePin>().ToList());
            yield return (nameof(Node.OutputDataPins), node.OutputDataPins.Cast<NodePin>().ToList());
            yield return (nameof(Node.InputExecPins), node.InputExecPins.Cast<NodePin>().ToList());
            yield return (nameof(Node.OutputExecPins), node.OutputExecPins.Cast<NodePin>().ToList());
            yield return (nameof(Node.InputTypePins), node.InputTypePins.Cast<NodePin>().ToList());
            yield return (nameof(Node.OutputTypePins), node.OutputTypePins.Cast<NodePin>().ToList());
        }

        private static string FormatValue(object? value) => value switch
        {
            null => "null",
            string text => $"\"{text}\"",
            _ => $"{Convert.ToString(value, CultureInfo.InvariantCulture)}:{value.GetType().Name}",
        };

        private static string TypeOf(NodePin pin) =>
            pin is NodeDataPin dataPin ? dataPin.PinType.Value?.FullCodeName ?? "?" : NoValue;

        private static string DescribeSlot(Node node, NodePin? pin)
        {
            if (pin is null)
            {
                return "null";
            }

            foreach (var (collection, pins) in CollectionsOf(node))
            {
                int index = pins.IndexOf(pin);

                if (index >= 0)
                {
                    return $"{collection}[{index}]";
                }
            }

            return "not in any collection";
        }

        private static List<string> Describe(string label, Node node)
        {
            var lines = new List<string>();

            foreach (var (collection, pins) in CollectionsOf(node))
            {
                for (int i = 0; i < pins.Count; i++)
                {
                    NodePin pin = pins[i];
                    string unconnected = pin is NodeInputDataPin input ? FormatValue(input.UnconnectedValue) : NoValue;
                    lines.Add($"{label} {collection}[{i}] {PinKeys.For(pin)} type={TypeOf(pin)} unconnected={unconnected}");
                }
            }

            if (lines.Count == 0)
            {
                lines.Add($"{label} <no pins>");
            }

            IEnumerable<PropertyInfo> properties = node.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => typeof(NodePin).IsAssignableFrom(p.PropertyType) && p.GetIndexParameters().Length == 0)
                .OrderBy(p => p.Name, StringComparer.Ordinal);

            foreach (PropertyInfo property in properties)
            {
                string slot;

                try
                {
                    slot = DescribeSlot(node, (NodePin?)property.GetValue(node));
                }
                catch (TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    slot = $"throws {ex.InnerException.GetType().Name}";
                }

                lines.Add($"{label}.{property.Name} -> {slot}");
            }

            return lines;
        }

        private static string LabelOf(Variant variant, Node node) => $"{node.GetType().Name}[{variant.Name}]";

        [Fact]
        public void PinLayoutMatchesGoldenFile()
        {
            var lines = new List<string>();

            foreach (Variant variant in BuildVariants())
            {
                Node node = variant.Build();
                lines.AddRange(Describe(LabelOf(variant, node), node));
            }

            string goldenPath = Path.Combine(SampleProjectFactory.FindRepositoryRoot(),
                "tests", "NetPrints.Core.Tests", "Fixtures", "Golden", GoldenFileName);

            if (Environment.GetEnvironmentVariable(GoldenCSharpTests.UpdateSnapshotsVariable) == "1")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(goldenPath) ?? throw new InvalidOperationException($"'{goldenPath}' has no directory."));
                File.WriteAllLines(goldenPath, lines);
            }

            Assert.True(File.Exists(goldenPath), $"Missing {goldenPath}; regenerate with {GoldenCSharpTests.UpdateSnapshotsVariable}=1");
            Assert.Equal(File.ReadAllLines(goldenPath), lines);
        }

        [Fact]
        public void TogglingPurityTwiceRestoresTheOriginalLayout()
        {
            List<IGrouping<string?, Variant>> groups = BuildVariants()
                .Where(v => v.PurityGroup is not null)
                .GroupBy(v => v.PurityGroup)
                .Where(g => g.Count() == 3)
                .ToList();

            Assert.NotEmpty(groups);

            foreach (IGrouping<string?, Variant> group in groups)
            {
                Variant[] members = group.ToArray();

                Assert.Equal(Describe("x", members[0].Build()), Describe("x", members[2].Build()));
            }
        }

        [Fact]
        public void InstanceIndexerSetterNewValuePinIsTheLastInputPin()
        {
            var setter = new VariableSetterNode(new MethodGraph("M"), Variable("this[]", VariableModifiers.None));

            Assert.Equal(3, setter.InputDataPins.Count);
            Assert.Same(setter.InputDataPins[2], setter.NewValuePin);
            Assert.NotSame(setter.IndexPin, setter.NewValuePin);
        }

        [Fact]
        public void SizePinOutsidePredefinedSizeModeThrowsInvalidOperationException()
        {
            var empty = new MakeArrayNode(new MethodGraph("M"));
            var withElements = new MakeArrayNode(new MethodGraph("M"));
            withElements.AddElementPin();

            Assert.Throws<InvalidOperationException>(() => empty.SizePin);
            Assert.Throws<InvalidOperationException>(() => withElements.SizePin);
        }
    }
}
