using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests.Translator
{
    /// <summary>
    /// <see cref="ClassTranslator.Translate(ClassGraph)"/>'s source map (compilation-and-diagnostics.md
    /// §2, research.md R3): RC-T06.
    /// </summary>
    public class SourceMapTests
    {
        [Fact]
        public void BuildingTheSourceMapDoesNotChangeTheGeneratedCode()
        {
            ClassGraph cls = BuildClassWithBadArgument(out _);

            string withoutMap = new ClassTranslator(TranslationEnvironment.BuiltIn).TranslateClass(cls);
            TranslatedClass withMap = new ClassTranslator(TranslationEnvironment.BuiltIn).Translate(cls);

            Assert.Equal(withoutMap, withMap.Code);
        }

        [Fact]
        public void EmptyClassHasAnEmptySourceMap()
        {
            var cls = new ClassGraph { Name = "EmptyFixture", Namespace = "NetPrints.Tests.Translator" };

            TranslatedClass translated = new ClassTranslator(TranslationEnvironment.BuiltIn).Translate(cls);

            Assert.Empty(translated.Map.Entries);
            Assert.Null(translated.Map.Find(0));
        }

        [Fact]
        public void ACSharpErrorInACallArgumentMapsToTheCallNode()
        {
            ClassGraph cls = BuildClassWithBadArgument(out string callNodeId);
            TranslatedClass translated = new ClassTranslator(TranslationEnvironment.BuiltIn).Translate(cls);

            IReadOnlyList<Diagnostic> errors = CompileAndGetErrors(translated.Code);
            Diagnostic error = Assert.Single(errors, d => d.Id == "CS1503");

            SourceMapEntry? entry = translated.Map.Find(error.Location.SourceSpan.Start);

            Assert.NotNull(entry);
            Assert.Equal(callNodeId, entry.Value.NodeId);
        }

        /// <summary>
        /// A class with one method: a call to <see cref="Console.WriteLine(string)"/> whose argument is
        /// wired to an <c>int</c> literal instead of a <c>string</c> one (the graph model does not itself
        /// enforce pin type compatibility), producing a genuine <c>CS1503</c> once compiled.
        /// </summary>
        private static ClassGraph BuildClassWithBadArgument(out string callNodeId)
        {
            var cls = new ClassGraph { Name = "SourceMapFixture", Namespace = "NetPrints.Tests.Translator" };
            var method = new MethodGraph("Run") { Class = cls, Visibility = MemberVisibility.Public };

            // Guid.Parse(string) has no int overload (unlike Console.WriteLine), so wiring an int
            // literal into its argument is a genuine CS1503, not just a different overload resolving.
            TypeSpecifier stringType = TypeSpecifier.FromType<string>();
            var parseSpecifier = new MethodSpecifier("Parse",
                new[] { new MethodParameter("input", stringType, MethodParameterPassType.Default, false, null) },
                Array.Empty<BaseType>(), MethodModifiers.Static, MemberVisibility.Public,
                TypeSpecifier.FromType<Guid>(), Array.Empty<BaseType>());

            var callNode = new CallMethodNode(method, parseSpecifier);
            var badArgument = LiteralNode.WithValue(method, 123);

            GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, callNode.InputExecPins[0]);
            GraphUtil.ConnectExecPins(callNode.OutputExecPins[0], method.ReturnNodes.First().InputExecPins[0]);
            GraphUtil.ConnectDataPins(badArgument.ValuePin, callNode.ArgumentPins[0]);

            cls.Methods.Add(method);
            callNodeId = callNode.Id;
            return cls;
        }

        private static IReadOnlyList<Diagnostic> CompileAndGetErrors(string source)
        {
            string trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
            IEnumerable<MetadataReference> references = trusted
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));

            CSharpCompilation compilation = CSharpCompilation.Create(
                "SourceMapFixture",
                [CSharpSyntaxTree.ParseText(source)],
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            return compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        }
    }
}
