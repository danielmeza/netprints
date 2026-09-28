using NetPrints.Compilation;
using NetPrints.Editor.ErrorList;

namespace NetPrints.Editor.Tests.ErrorList;

/// <summary><see cref="DiagnosticRowVM"/> (FR-032, OWN-03: severity icon/color state).</summary>
public sealed class DiagnosticRowVMTests
{
    private static CodeDiagnostic Diagnostic(CodeDiagnosticSeverity severity) =>
        new(severity, "CS0001", "boom", "N.C", null, null, null, null);

    [Theory]
    [InlineData(CodeDiagnosticSeverity.Error, true, false, false)]
    [InlineData(CodeDiagnosticSeverity.Warning, false, true, false)]
    [InlineData(CodeDiagnosticSeverity.Info, false, false, true)]
    public void SeverityFlagsMatchTheDiagnosticsSeverity(CodeDiagnosticSeverity severity, bool isError, bool isWarning, bool isInfo)
    {
        var row = new DiagnosticRowVM(Diagnostic(severity), null);

        Assert.Equal(isError, row.IsError);
        Assert.Equal(isWarning, row.IsWarning);
        Assert.Equal(isInfo, row.IsInfo);
    }
}
