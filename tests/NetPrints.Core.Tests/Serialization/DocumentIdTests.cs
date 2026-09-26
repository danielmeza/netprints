using System;
using NetPrints.Serialization;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary>Tests for <see cref="DocumentId"/> and <see cref="DiagnosticExtensions.ToDiagnostic"/>.</summary>
    public class DocumentIdTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("/rooted")]
        [InlineData("a\\b")]
        [InlineData("a/../b")]
        [InlineData("./a")]
        [InlineData("a/.")]
        public void ConstructorRejectsInvalidPaths(string path)
        {
            Assert.Throws<ArgumentException>(() => new DocumentId(path));
        }

        [Fact]
        public void PathAndFileNameRoundTrip()
        {
            var id = new DocumentId("Foo/Bar.netpc.json");

            Assert.Equal("Foo/Bar.netpc.json", id.Path);
            Assert.Equal("Bar.netpc.json", id.FileName);
            Assert.Equal("Foo/Bar.netpc.json", id.ToString());
        }

        [Fact]
        public void FileNameWithNoDirectoryIsTheWholePath()
        {
            var id = new DocumentId("Root.netpc.json");

            Assert.Equal("Root.netpc.json", id.FileName);
        }

        [Fact]
        public void SiblingKeepsTheDirectory()
        {
            var id = new DocumentId("Foo/Bar.netpc.json");

            Assert.Equal("Foo/Bar.netpc.g.cs", id.Sibling("Bar.netpc.g.cs").Path);
        }

        [Fact]
        public void SiblingOfARootDocumentHasNoDirectory()
        {
            var id = new DocumentId("Root.netpc.json");

            Assert.Equal("Other.netpc.json", id.Sibling("Other.netpc.json").Path);
        }

        [Fact]
        public void EqualityIsOrdinalAndCasePreserving()
        {
            Assert.Equal(new DocumentId("Foo.json"), new DocumentId("Foo.json"));
            Assert.NotEqual(new DocumentId("Foo.json"), new DocumentId("foo.json"));
        }

        [Fact]
        public void ToDiagnosticMapsSeverityCodeMessageAndDocumentPath()
        {
            var issue = new DocumentIssue(DocumentIssueSeverity.Warning, DocumentIssue.ConnectionDropped,
                "dropped", new DocumentId("Foo.netpc.json"));

            var diagnostic = issue.ToDiagnostic();

            Assert.Equal(NetPrints.Compilation.CodeDiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Equal(DocumentIssue.ConnectionDropped, diagnostic.Id);
            Assert.Equal("dropped", diagnostic.Message);
            Assert.Equal("Foo.netpc.json", diagnostic.SourcePath);
            Assert.Null(diagnostic.ClassFullName);
            Assert.Null(diagnostic.GraphKey);
            Assert.Null(diagnostic.NodeId);
            Assert.Null(diagnostic.Span);
        }

        [Theory]
        [InlineData(DocumentIssueSeverity.Info, NetPrints.Compilation.CodeDiagnosticSeverity.Info)]
        [InlineData(DocumentIssueSeverity.Warning, NetPrints.Compilation.CodeDiagnosticSeverity.Warning)]
        [InlineData(DocumentIssueSeverity.Error, NetPrints.Compilation.CodeDiagnosticSeverity.Error)]
        public void ToDiagnosticMapsEverySeverity(DocumentIssueSeverity severity, NetPrints.Compilation.CodeDiagnosticSeverity expected)
        {
            var issue = new DocumentIssue(severity, "NPD001", "message", null);

            Assert.Equal(expected, issue.ToDiagnostic().Severity);
        }
    }
}
