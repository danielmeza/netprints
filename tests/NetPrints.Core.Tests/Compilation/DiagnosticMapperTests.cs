using System.Collections.Generic;
using NetPrints.Compilation;
using NetPrints.Projects;
using Xunit;

namespace NetPrints.Tests.Compilation
{
    /// <summary>
    /// <see cref="DiagnosticMapper.FromBuild"/> as of T061: every <see cref="ProjectMessage"/> maps to
    /// one <see cref="CodeDiagnostic"/>, with its severity, code, message, file and 1-based line/column
    /// converted to a 0-based span; the source-map half of RC-T10 is T090.
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
    }
}
