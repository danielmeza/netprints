using System;
using System.Linq;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Mapping.BuiltIn;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary>
    /// Converter-level (not full-document) round trips for each built-in <see cref="INodeDocumentConverter"/>
    /// of <see cref="NodeDocumentConverterRegistry.BuiltIn"/> (T031-T034). DF-T06's full-document round
    /// trip (pins, connections, positions) is in DocumentMapperTests.
    /// </summary>
    public class BuiltInConverterTests
    {
        private static INodeDocumentConverter Converter(string kind) =>
            NodeDocumentConverterRegistry.BuiltIn.Single(c => c.Kind == kind);

        private static ClassGraph NewClass() => new ClassGraph { Name = "C" };

        [Fact]
        public void MethodEntryRoundTripsArgumentAndGenericArgumentCounts()
        {
            var cls = NewClass();
            var method = new MethodGraph("M") { Class = cls };
            var entry = (MethodEntryNode)method.EntryNode;
            entry.AddArgument();
            entry.AddArgument();
            entry.AddGenericArgument();
            entry.OutputTypePins[0].Name = "TItem";

            var context = new NodeMappingContext(cls);
            var converter = Converter("methodEntry");
            var doc = (MethodEntryNodeDocument)converter.ToDocument(entry, context);

            Assert.Equal(2, doc.ArgumentCount);
            Assert.Equal(["TItem"], doc.GenericArguments);

            var method2 = new MethodGraph("M") { Class = cls };
            var created = (MethodEntryNode)converter.CreateNode(doc, method2, context);

            Assert.Same(method2.EntryNode, created);
            Assert.Equal(2, created.OutputDataPins.Count);
            Assert.Equal(["TItem"], created.OutputTypePins.Select(p => p.Name));
        }

        [Fact]
        public void ConstructorEntryRoundTripsZeroArguments()
        {
            var cls = NewClass();
            var ctor = new ConstructorGraph { Class = cls };
            var context = new NodeMappingContext(cls);
            var converter = Converter("constructorEntry");

            var doc = (ConstructorEntryNodeDocument)converter.ToDocument(ctor.EntryNode, context);
            Assert.Equal(0, doc.ArgumentCount);

            var ctor2 = new ConstructorGraph { Class = cls };
            var created = converter.CreateNode(doc, ctor2, context);
            Assert.Same(ctor2.EntryNode, created);
        }

        [Fact]
        public void ConstructorEntryWithArgumentsThrows()
        {
            var cls = NewClass();
            var ctor = new ConstructorGraph { Class = cls };
            var context = new NodeMappingContext(cls);
            var converter = Converter("constructorEntry");
            var doc = new ConstructorEntryNodeDocument("n0", null, null, 1);

            Assert.Throws<DocumentFormatException>(() => converter.CreateNode(doc, ctor, context));
        }

        [Fact]
        public void ReturnClaimsTheMainReturnNodeOnceThenCreatesFreshOnes()
        {
            var cls = NewClass();
            var method = new MethodGraph("M") { Class = cls };
            method.MainReturnNode.AddReturnType();
            var context = new NodeMappingContext(cls);
            var converter = Converter("return");

            var mainDoc = (ReturnNodeDocument)converter.ToDocument(method.MainReturnNode, context);
            Assert.Equal(1, mainDoc.ReturnCount);

            var method2 = new MethodGraph("M") { Class = cls };
            var firstCreated = converter.CreateNode(mainDoc, method2, context);
            Assert.Same(method2.MainReturnNode, firstCreated);
            Assert.Single(method2.MainReturnNode.InputDataPins);

            var secondDoc = new ReturnNodeDocument("n9", null, null, 1);
            var secondCreated = (ReturnNode)converter.CreateNode(secondDoc, method2, context);
            Assert.NotSame(method2.MainReturnNode, secondCreated);
            Assert.Equal(method2.MainReturnNode.InputDataPins.Count, secondCreated.InputDataPins.Count);
        }

        [Fact]
        public void ClassReturnRoundTripsInterfaceCount()
        {
            var cls = NewClass();
            cls.ReturnNode.AddInterfacePin();
            cls.ReturnNode.AddInterfacePin();
            var context = new NodeMappingContext(cls);
            var converter = Converter("classReturn");

            var doc = (ClassReturnNodeDocument)converter.ToDocument(cls.ReturnNode, context);
            Assert.Equal(2, doc.InterfaceCount);

            var cls2 = NewClass();
            var created = converter.CreateNode(doc, cls2, context);
            Assert.Same(cls2.ReturnNode, created);
            Assert.Equal(2, cls2.ReturnNode.InterfacePins.Count());
        }

        [Fact]
        public void TypeReturnFetchesTheExistingNode()
        {
            var typeGraph = new TypeGraph();
            var context = new NodeMappingContext(NewClass());
            var converter = Converter("typeReturn");

            var doc = converter.ToDocument(typeGraph.ReturnNode, context);
            var created = converter.CreateNode(doc, typeGraph, context);

            Assert.Same(typeGraph.ReturnNode, created);
        }

        [Fact]
        public void CallMethodRoundTrips()
        {
            var cls = NewClass();
            var method = new MethodGraph("M") { Class = cls };
            var writeLine = new MethodSpecifier("WriteLine",
                [new MethodParameter("value", TypeSpecifier.FromType<string>(), MethodParameterPassType.Default, false, null)],
                [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType(typeof(Console)), []);
            var call = new CallMethodNode(method, writeLine);
            var context = new NodeMappingContext(cls);
            var converter = Converter("callMethod");

            var doc = (CallMethodNodeDocument)converter.ToDocument(call, context);
            Assert.Equal("WriteLine", doc.Method.Name);

            var created = (CallMethodNode)converter.CreateNode(doc, method, context);
            Assert.Equal(writeLine, created.MethodSpecifier);
        }

        [Fact]
        public void ConstructorNodeRoundTrips()
        {
            var cls = NewClass();
            var method = new MethodGraph("M") { Class = cls };
            var specifier = new ConstructorSpecifier([], TypeSpecifier.FromType<Exception>());
            var node = new ConstructorNode(method, specifier);
            var context = new NodeMappingContext(cls);
            var converter = Converter("constructor");

            var doc = (ConstructorNodeDocument)converter.ToDocument(node, context);
            var created = (ConstructorNode)converter.CreateNode(doc, method, context);

            Assert.Equal(specifier.DeclaringType, created.ConstructorSpecifier.DeclaringType);
        }

        [Fact]
        public void MakeDelegateRoundTrips()
        {
            var cls = NewClass();
            var method = new MethodGraph("M") { Class = cls };
            var writeLine = new MethodSpecifier("WriteLine",
                [new MethodParameter("value", TypeSpecifier.FromType<string>(), MethodParameterPassType.Default, false, null)],
                [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType(typeof(Console)), []);
            var node = new MakeDelegateNode(method, writeLine);
            var context = new NodeMappingContext(cls);
            var converter = Converter("makeDelegate");

            var doc = (MakeDelegateNodeDocument)converter.ToDocument(node, context);
            var created = (MakeDelegateNode)converter.CreateNode(doc, method, context);

            Assert.Equal(writeLine, created.MethodSpecifier);
        }

        [Fact]
        public void VariableGetterAndSetterRoundTrip()
        {
            var cls = NewClass();
            var method = new MethodGraph("M") { Class = cls };
            var variable = new VariableSpecifier("Items", TypeSpecifier.FromType<int>(), MemberVisibility.Public,
                MemberVisibility.Public, new TypeSpecifier("C"), VariableModifiers.Static);
            var getter = new VariableGetterNode(method, variable);
            var setter = new VariableSetterNode(method, variable);
            var context = new NodeMappingContext(cls);

            var getterConverter = Converter("variableGetter");
            var getterDoc = (VariableGetterNodeDocument)getterConverter.ToDocument(getter, context);
            var createdGetter = (VariableGetterNode)getterConverter.CreateNode(getterDoc, method, context);
            Assert.Equal("Items", createdGetter.VariableName);

            var setterConverter = Converter("variableSetter");
            var setterDoc = (VariableSetterNodeDocument)setterConverter.ToDocument(setter, context);
            var createdSetter = (VariableSetterNode)setterConverter.CreateNode(setterDoc, method, context);
            Assert.Equal("Items", createdSetter.VariableName);
        }

        [Fact]
        public void LiteralRoundTrips()
        {
            var cls = NewClass();
            var method = new MethodGraph("M") { Class = cls };
            var node = new LiteralNode(method, TypeSpecifier.FromType<int>());
            var context = new NodeMappingContext(cls);
            var converter = Converter("literal");

            var doc = (LiteralNodeDocument)converter.ToDocument(node, context);
            Assert.Equal("System.Int32", doc.LiteralType.Name);

            var created = (LiteralNode)converter.CreateNode(doc, method, context);
            Assert.Equal(node.LiteralType, created.LiteralType);
        }

        [Fact]
        public void TypeNodeRoundTripsGenericParameter()
        {
            var cls = NewClass();
            var method = new MethodGraph("M") { Class = cls };
            var node = new TypeNode(method, new GenericType("T"));
            var context = new NodeMappingContext(cls);
            var converter = Converter("type");

            var doc = (TypeNodeDocument)converter.ToDocument(node, context);
            Assert.True(doc.Type.Generic);

            var created = (TypeNode)converter.CreateNode(doc, method, context);
            Assert.IsType<GenericType>(created.Type);
        }

        [Fact]
        public void MakeArrayTypeRoundTrips()
        {
            var cls = NewClass();
            var method = new MethodGraph("M") { Class = cls };
            var node = new MakeArrayTypeNode(method);
            var context = new NodeMappingContext(cls);
            var converter = Converter("makeArrayType");

            var doc = converter.ToDocument(node, context);
            var created = converter.CreateNode(doc, method, context);

            Assert.IsType<MakeArrayTypeNode>(created);
        }

        [Fact]
        public void MakeArrayWithPredefinedSizeRoundTrips()
        {
            var cls = NewClass();
            var method = new MethodGraph("M") { Class = cls };
            var node = new MakeArrayNode(method) { UsePredefinedSize = true };
            var context = new NodeMappingContext(cls);
            var converter = Converter("makeArray");

            var doc = (MakeArrayNodeDocument)converter.ToDocument(node, context);
            Assert.True(doc.UsePredefinedSize);
            Assert.Equal(0, doc.ElementCount);

            var created = (MakeArrayNode)converter.CreateNode(doc, method, context);
            Assert.True(created.UsePredefinedSize);
            Assert.Single(created.InputDataPins);
        }

        [Fact]
        public void MakeArrayWithElementsRoundTrips()
        {
            var cls = NewClass();
            var method = new MethodGraph("M") { Class = cls };
            var node = new MakeArrayNode(method);
            node.AddElementPin();
            node.AddElementPin();
            node.AddElementPin();
            var context = new NodeMappingContext(cls);
            var converter = Converter("makeArray");

            var doc = (MakeArrayNodeDocument)converter.ToDocument(node, context);
            Assert.False(doc.UsePredefinedSize);
            Assert.Equal(3, doc.ElementCount);

            var created = (MakeArrayNode)converter.CreateNode(doc, method, context);
            Assert.False(created.UsePredefinedSize);
            Assert.Equal(3, created.InputDataPins.Count);
        }

        [Theory]
        [InlineData("explicitCast", typeof(ExplicitCastNode))]
        [InlineData("typeOf", typeof(TypeOfNode))]
        [InlineData("default", typeof(DefaultNode))]
        [InlineData("ifElse", typeof(IfElseNode))]
        [InlineData("forLoop", typeof(ForLoopNode))]
        [InlineData("ternary", typeof(TernaryNode))]
        [InlineData("await", typeof(AwaitNode))]
        [InlineData("throw", typeof(ThrowNode))]
        public void FieldlessKindsRoundTrip(string kind, Type expectedType)
        {
            var cls = NewClass();
            var method = new MethodGraph("M") { Class = cls };
            var context = new NodeMappingContext(cls);
            var converter = Converter(kind);
            var node = (Node)Activator.CreateInstance(expectedType, method)!;

            var doc = converter.ToDocument(node, context);
            var created = converter.CreateNode(doc, method, context);

            Assert.IsType(expectedType, created);
        }

        [Fact]
        public void RerouteExecRoundTrips()
        {
            var cls = NewClass();
            var method = new MethodGraph("M") { Class = cls };
            var node = RerouteNode.MakeExecution(method, 2);
            var context = new NodeMappingContext(cls);
            var converter = Converter("reroute");

            var doc = (RerouteNodeDocument)converter.ToDocument(node, context);
            Assert.Equal("exec", doc.PinKind);
            Assert.Equal(2, doc.Count);
            Assert.Null(doc.DataTypes);

            var created = (RerouteNode)converter.CreateNode(doc, method, context);
            Assert.Equal(2, created.ExecRerouteCount);
        }

        [Fact]
        public void RerouteTypeRoundTrips()
        {
            var cls = NewClass();
            var method = new MethodGraph("M") { Class = cls };
            var node = RerouteNode.MakeType(method, 1);
            var context = new NodeMappingContext(cls);
            var converter = Converter("reroute");

            var doc = (RerouteNodeDocument)converter.ToDocument(node, context);
            Assert.Equal("type", doc.PinKind);
            Assert.Equal(1, doc.Count);

            var created = (RerouteNode)converter.CreateNode(doc, method, context);
            Assert.Equal(1, created.TypeRerouteCount);
        }

        [Fact]
        public void RerouteDataRoundTrips()
        {
            var cls = NewClass();
            var method = new MethodGraph("M") { Class = cls };
            var node = RerouteNode.MakeData(method,
                [Tuple.Create<BaseType, BaseType>(TypeSpecifier.FromType<int>(), TypeSpecifier.FromType<int>())]);
            var context = new NodeMappingContext(cls);
            var converter = Converter("reroute");

            var doc = (RerouteNodeDocument)converter.ToDocument(node, context);
            Assert.Equal("data", doc.PinKind);
            Assert.Equal(0, doc.Count);
            Assert.NotNull(doc.DataTypes);
            Assert.Single(doc.DataTypes!);
            Assert.Equal("System.Int32", doc.DataTypes![0][0].Name);

            var created = (RerouteNode)converter.CreateNode(doc, method, context);
            Assert.Equal(1, created.DataRerouteCount);
        }

        [Fact]
        public void RerouteUnknownPinKindThrows()
        {
            var cls = NewClass();
            var method = new MethodGraph("M") { Class = cls };
            var context = new NodeMappingContext(cls);
            var converter = Converter("reroute");
            var doc = new RerouteNodeDocument("n0", null, null, "bogus", 0, null);

            Assert.Throws<DocumentFormatException>(() => converter.CreateNode(doc, method, context));
        }
    }
}
