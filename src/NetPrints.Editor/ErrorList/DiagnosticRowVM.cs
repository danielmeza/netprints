using NetPrints.Compilation;
using NetPrints.Core;

namespace NetPrints.Editor.ErrorList;

/// <summary>
/// One row of the class editor's Errors tab (FR-032): a <see cref="CodeDiagnostic"/> from either the
/// live analysis or the last build, plus the display name of the member it belongs to, resolved from
/// its <see cref="CodeDiagnostic.GraphKey"/> against the class that reported it. A diagnostic with no
/// source-map entry is still listed, just not navigable (<see cref="CanNavigate"/>).
/// </summary>
public sealed class DiagnosticRowVM
{
    /// <summary>
    /// Wraps <paramref name="diagnostic"/>, resolving <see cref="MemberName"/> against
    /// <paramref name="owner"/> when both are known.
    /// </summary>
    /// <param name="diagnostic">The diagnostic to show.</param>
    /// <param name="owner">The class that reported the diagnostic, for resolving
    /// <see cref="CodeDiagnostic.GraphKey"/>'s member name, or <see langword="null"/> if not known.</param>
    public DiagnosticRowVM(CodeDiagnostic diagnostic, ClassGraph? owner)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        Diagnostic = diagnostic;
        MemberName = owner is not null && diagnostic.GraphKey is { } key && GraphKeys.Resolve(owner, key) is { } graph and not ClassGraph
            ? graph.ToString()
            : null;
    }

    /// <summary>The wrapped diagnostic.</summary>
    public CodeDiagnostic Diagnostic { get; }

    /// <summary>Severity of the diagnostic.</summary>
    public CodeDiagnosticSeverity Severity => Diagnostic.Severity;

    /// <summary>Whether <see cref="Severity"/> is <see cref="CodeDiagnosticSeverity.Error"/> (FR-032, OWN-03: drives the row's severity icon/color).</summary>
    public bool IsError => Severity == CodeDiagnosticSeverity.Error;

    /// <summary>Whether <see cref="Severity"/> is <see cref="CodeDiagnosticSeverity.Warning"/> (FR-032, OWN-03).</summary>
    public bool IsWarning => Severity == CodeDiagnosticSeverity.Warning;

    /// <summary>Whether <see cref="Severity"/> is <see cref="CodeDiagnosticSeverity.Info"/> (FR-032, OWN-03).</summary>
    public bool IsInfo => Severity == CodeDiagnosticSeverity.Info;

    /// <summary>Stable machine-readable code (e.g. <c>"CS0103"</c>).</summary>
    public string Id => Diagnostic.Id;

    /// <summary>Human-readable description.</summary>
    public string Message => Diagnostic.Message;

    /// <summary>Full name of the class the diagnostic belongs to, if known.</summary>
    public string? ClassFullName => Diagnostic.ClassFullName;

    /// <summary>Display name of the method, constructor or event graph the diagnostic belongs to, if known.</summary>
    public string? MemberName { get; }

    /// <summary>Whether double-clicking this row can open a graph and select a node (FR-034).</summary>
    public bool CanNavigate => Diagnostic.GraphKey is not null && Diagnostic.NodeId is not null;
}
