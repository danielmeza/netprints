using NetPrints.Compilation;
using NetPrints.Editor.ErrorList;

namespace NetPrints.Editor.Tests.ErrorList;

/// <summary><see cref="DiagnosticRowViewModel"/> (FR-032, OWN-03: severity icon/color state).</summary>
public sealed class DiagnosticRowViewModelTests
{
    private static CodeDiagnostic Diagnostic(CodeDiagnosticSeverity severity) =>
        new(severity, "CS0001", "boom", "N.C", null, null, null, null);

    [Theory]
    [InlineData(CodeDiagnosticSeverity.Error, true, false, false)]
    [InlineData(CodeDiagnosticSeverity.Warning, false, true, false)]
    [InlineData(CodeDiagnosticSeverity.Info, false, false, true)]
    public void SeverityFlagsMatchTheDiagnosticsSeverity(CodeDiagnosticSeverity severity, bool isError, bool isWarning, bool isInfo)
    {
        var row = new DiagnosticRowViewModel(Diagnostic(severity), null);

        Assert.Equal(isError, row.IsError);
        Assert.Equal(isWarning, row.IsWarning);
        Assert.Equal(isInfo, row.IsInfo);
    }

    [Fact]
    public void CanNavigateIsFalseWithNoGraphKeyAndNoClass()
    {
        var row = new DiagnosticRowViewModel(new CodeDiagnostic(CodeDiagnosticSeverity.Error, "CS0001", "boom", null, null, null, null, null), null);

        Assert.False(row.CanNavigate);
    }

    [Fact]
    public void CanNavigateIsTrueWithAClassButNoGraphKey()
    {
        var row = new DiagnosticRowViewModel(Diagnostic(CodeDiagnosticSeverity.Error), null);

        Assert.True(row.CanNavigate);
    }

    [Fact]
    public void CanNavigateIsTrueWithAGraphKeyEvenWithoutANode()
    {
        // OWN-04 (FR-034, ED-T03): a diagnostic whose position maps to no node (e.g. one reported past
        // the last mapped statement) still knows its member and should still open the graph.
        var diagnostic = new CodeDiagnostic(CodeDiagnosticSeverity.Error, "CS0161", "boom", "N.C", "graphKey", null, null, null);

        var row = new DiagnosticRowViewModel(diagnostic, null);

        Assert.True(row.CanNavigate);
    }
}
