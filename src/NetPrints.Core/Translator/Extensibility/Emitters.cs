#nullable enable
namespace NetPrints.Translator;

/// <summary>
/// Adds usings, attributes, modifiers and base types to a translated type declaration
/// (extension-points.md §3).
/// </summary>
public interface IClassEmitter
{
    /// <summary>
    /// Stable id, used in <c>NPT005</c> messages.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Edits <paramref name="context"/>; called once per translated declaration before its members.
    /// </summary>
    /// <param name="context">The declaration's emit state.</param>
    void EmitClass(ClassEmitContext context);
}

/// <summary>
/// Adds attributes and modifiers to a translated member, or turns a property into a partial declaration
/// (extension-points.md §3).
/// </summary>
public interface IMemberEmitter
{
    /// <summary>
    /// Stable id, used in <c>NPT005</c> messages.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Edits <paramref name="context"/>; called once per translated member.
    /// </summary>
    /// <param name="context">The member's emit state.</param>
    void EmitMember(MemberEmitContext context);
}

/// <summary>
/// The kind of member a <see cref="MemberEmitContext"/> describes.
/// </summary>
public enum EmittedMemberKind
{
    /// <summary>A variable without accessors.</summary>
    Field,

    /// <summary>A variable with getter or setter graphs.</summary>
    Property,

    /// <summary>A method graph.</summary>
    Method,

    /// <summary>A constructor graph.</summary>
    Constructor,

    /// <summary>The method an event entry node produces.</summary>
    EventMethod,
}
