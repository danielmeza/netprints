#nullable enable

namespace NetPrints.Serialization;

/// <summary>
/// Kind of document a <see cref="DocumentId"/> refers to.
/// </summary>
public enum DocumentKind
{
    /// <summary>The project file (<c>.csproj</c>).</summary>
    Project,

    /// <summary>A class graph document (<c>.netpc.json</c>, or legacy <c>.netpc</c>).</summary>
    Class,
}

/// <summary>
/// Severity of a <see cref="DocumentIssue"/>.
/// </summary>
public enum DocumentIssueSeverity
{
    /// <summary>Informational; nothing was dropped or changed unexpectedly.</summary>
    Info,

    /// <summary>Something was dropped or ignored, but the rest of the document loaded.</summary>
    Warning,

    /// <summary>The document (or part of it) could not be loaded at all.</summary>
    Error,
}

/// <summary>
/// A problem found while reading or mapping a document: an unknown node kind preserved, a dropped
/// connection or pin state, an ignored layout entry, or a similar non-fatal condition
/// (document-format.md §2.1). Fatal problems are reported as a <see cref="DocumentFormatException"/>
/// instead.
/// </summary>
/// <param name="Severity">How serious the issue is.</param>
/// <param name="Code">Stable machine-readable code (<c>NPD001</c>–<c>NPD007</c>).</param>
/// <param name="Message">Human-readable description.</param>
/// <param name="Document">Document the issue was found in, if known.</param>
public sealed record DocumentIssue(DocumentIssueSeverity Severity, string Code, string Message, DocumentId? Document)
{
    /// <summary>Node of unknown kind (missing or untrusted extension) preserved unchanged.</summary>
    public const string UnknownNodeKind = "NPD001";

    /// <summary>Connection dropped: missing node, unknown pin key, or incompatible pins.</summary>
    public const string ConnectionDropped = "NPD002";

    /// <summary>Pin state dropped: unknown pin key.</summary>
    public const string PinStateDropped = "NPD003";

    /// <summary>Layout entry ignored: unknown graph key or node id.</summary>
    public const string LayoutEntryIgnored = "NPD004";

    /// <summary>Unknown <c>NetPrintsProfile</c>; the default profile was used.</summary>
    public const string UnknownProfile = "NPD005";

    /// <summary>Project extension not trusted; its nodes were preserved but are inactive.</summary>
    public const string ExtensionNotTrusted = "NPD006";

    /// <summary>
    /// A node or member id was duplicated within its scope of uniqueness (a graph for node ids, the
    /// class for member ids) — a merge, or a hand-edited or copy-pasted file. The later occurrence (in
    /// document order) was reassigned a fresh id; the document still loaded.
    /// </summary>
    public const string DuplicateIdReassigned = "NPD007";

    /// <summary>
    /// A graph document could not be read at all: malformed JSON or XML, an unsupported extension, or a
    /// deserialization failure (<see cref="DocumentFormatException"/>). Unlike the other codes in this
    /// class, this one is <see cref="DocumentIssueSeverity.Error"/>: the document did not load, and a
    /// caller that keeps working after skipping it (<c>GraphCodeGenerator</c>, <c>ProjectPersistence</c>)
    /// reports it as an issue instead of propagating the exception.
    /// </summary>
    public const string DocumentUnreadable = "NPD008";
}
