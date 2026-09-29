using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Projects;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests.Compilation
{
    /// <summary>
    /// <see cref="DiagnosticMapper.FromBuild"/> (T061), its source-map half, <see cref="DiagnosticMapper.FromRoslyn"/>
    /// and <see cref="DiagnosticMapper.FromTranslation"/> (T090, RC-T10): every message/diagnostic/failure
    /// maps to one <see cref="CodeDiagnostic"/>, with severity, code, message, file and a 1-based
    /// line/column converted to a 0-based span; a generator message's <c>(graph key, node id)</c> suffix
    /// is parsed and stripped, and a compiler message or diagnostic against a generated file is resolved
    /// through that class's <see cref="SourceMap"/> when one is available.
    /// </summary>
    public class DiagnosticMapperTests
    {
        [Fact]
        public void MapsSeverityCodeMessageFileAndPosition()
        {
            var messages = new List<ProjectMessage>
            {
                new(ProjectMessageSeverity.Error, "CS1002", "; expected", "/repo/Program.cs", 12, 34),
                new(ProjectMessageSeverity.Warning, "NU1701", "Old package", null, null, null),
                new(ProjectMessageSeverity.Info, "MSB0001", "Note", "/repo/App.csproj", 5, null),
            };

            IReadOnlyList<CodeDiagnostic> diagnostics = DiagnosticMapper.FromBuild(messages);

            Assert.Equal(3, diagnostics.Count);

            Assert.Equal(CodeDiagnosticSeverity.Error, diagnostics[0].Severity);
            Assert.Equal("CS1002", diagnostics[0].Id);
            Assert.Equal("; expected", diagnostics[0].Message);
            Assert.Equal("/repo/Program.cs", diagnostics[0].SourcePath);
            Assert.Equal(11, diagnostics[0].Span?.Start.Line);
            Assert.Equal(33, diagnostics[0].Span?.Start.Character);

            Assert.Equal(CodeDiagnosticSeverity.Warning, diagnostics[1].Severity);
            Assert.Null(diagnostics[1].SourcePath);
            Assert.Null(diagnostics[1].Span);

            Assert.Equal(CodeDiagnosticSeverity.Info, diagnostics[2].Severity);
            Assert.Equal(4, diagnostics[2].Span?.Start.Line);
            Assert.Equal(0, diagnostics[2].Span?.Start.Character);
        }

        [Fact]
        public void NoMessagesMapsToNoDiagnostics() =>
            Assert.Empty(DiagnosticMapper.FromBuild([]));

        [Fact]
        public void StripsAndParsesTheGraphSuffix()
        {
            var messages = new List<ProjectMessage>
            {
                new(ProjectMessageSeverity.Error, "NPT005", "emitter failed (graph method1, node node7)", "/repo/Foo.netpc.g.cs", 3, 1),
            };

            CodeDiagnostic diagnostic = Assert.Single(DiagnosticMapper.FromBuild(messages));

            Assert.Equal("emitter failed", diagnostic.Message);
            Assert.Equal("method1", diagnostic.GraphKey);
            Assert.Equal("node7", diagnostic.NodeId);
        }

        [Fact]
        public void MapsAGeneratedFileMessageThroughItsClassSourceMap()
        {
            const string code = "class Foo\n{\n    void Run()\n    {\n        Bad();\n    }\n}\n";
            var entry = new SourceMapEntry(new TextSpan(code.IndexOf("Bad()", StringComparison.Ordinal), 5), "method1", "node7");
            var translated = new TranslatedClass("Foo", code, new SourceMap([entry]));
            var cls = new ClassGraph { Name = "Foo" };
            var classesByGeneratedPath = new Dictionary<string, (ClassGraph Class, TranslatedClass Translated)>
            {
                ["/repo/Foo.netpc.g.cs"] = (cls, translated),
            };

            var messages = new List<ProjectMessage>
            {
                new(ProjectMessageSeverity.Error, "CS0103", "The name 'Bad' does not exist", "/repo/Foo.netpc.g.cs", 5, 9),
            };

            CodeDiagnostic diagnostic = Assert.Single(DiagnosticMapper.FromBuild(messages, classesByGeneratedPath));

            Assert.Equal("Foo", diagnostic.ClassFullName);
            Assert.Equal("method1", diagnostic.GraphKey);
            Assert.Equal("node7", diagnostic.NodeId);
        }

        [Fact]
        public void FromRoslynResolvesTheNodeThroughTheSourceMap()
        {
            const string code = "class Foo { void Run() { Bad(); } }";
            int position = code.IndexOf("Bad", StringComparison.Ordinal);
            var map = new SourceMap([new SourceMapEntry(new TextSpan(position, 5), "method1", "node7")]);

            SyntaxTree tree = CSharpSyntaxTree.ParseText(code, path: "Foo.netpc.g.cs", cancellationToken: TestContext.Current.CancellationToken);
            var descriptor = new DiagnosticDescriptor("CS0103", "title", "The name 'Bad' does not exist", "Compiler", DiagnosticSeverity.Error, isEnabledByDefault: true);
            Diagnostic diagnostic = Diagnostic.Create(descriptor, Location.Create(tree, new TextSpan(position, 3)));
            var cls = new ClassGraph { Name = "Foo" };

            CodeDiagnostic mapped = DiagnosticMapper.FromRoslyn(diagnostic, cls, map);

            Assert.Equal(CodeDiagnosticSeverity.Error, mapped.Severity);
            Assert.Equal("CS0103", mapped.Id);
            Assert.Equal("Foo", mapped.ClassFullName);
            Assert.Equal("method1", mapped.GraphKey);
            Assert.Equal("node7", mapped.NodeId);
            Assert.Equal("Foo.netpc.g.cs", mapped.SourcePath);
        }

        [Fact]
        public void FromTranslationCarriesTheExceptionFields()
        {
            var exception = new TranslationException("NPT006", "No translator", "method1", "node7");
            var cls = new ClassGraph { Name = "Foo" };

            CodeDiagnostic mapped = DiagnosticMapper.FromTranslation(exception, cls);

            Assert.Equal(CodeDiagnosticSeverity.Error, mapped.Severity);
            Assert.Equal("NPT006", mapped.Id);
            Assert.Equal("No translator", mapped.Message);
            Assert.Equal("Foo", mapped.ClassFullName);
            Assert.Equal("method1", mapped.GraphKey);
            Assert.Equal("node7", mapped.NodeId);
        }
    }
}
