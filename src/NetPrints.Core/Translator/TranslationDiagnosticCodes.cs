namespace NetPrints.Translator;

/// <summary>
/// The stable <c>NPT</c> codes a <see cref="TranslationException"/> carries (compilation-and-diagnostics.md §2).
/// <c>NPT003</c> is a code generator diagnostic, declared as <c>GraphCodeGenerator.MissingExtensionCode</c> in
/// <c>NetPrints.Generator</c> instead of here.
/// </summary>
public static class TranslationDiagnosticCodes
{
    /// <summary>
    /// A translation failure the caller has not yet mapped to a coded <see cref="TranslationException"/>
    /// (an interim id until it does, e.g. <c>MainEditorVM.CompileAsync</c>'s <c>ClassTranslationFailure</c>
    /// handler).
    /// </summary>
    public const string Unclassified = "NPT000";

    /// <summary>An impure node used outside its own event entry's exec flow: it belongs to a different
    /// event entry of the same graph, so its value can never be computed here.</summary>
    public const string CrossEntryDependency = "NPT001";

    /// <summary>An event and a method on the same class share a name; they share one member-name
    /// namespace on the generated class.</summary>
    public const string DuplicateMemberName = "NPT002";

    /// <summary>A method-local variable's name (US5, sub-phase H) conflicts with a parameter name,
    /// another local of the same graph, or a reserved C# keyword.
    /// <see cref="NetPrints.Core.ExecutionGraph.IsLocalNameAvailable"/> keeps the editor from creating
    /// one; this is the translator's own defensive re-check, for a graph built or edited outside the
    /// editor's gate.</summary>
    public const string LocalVariableNameConflict = "NPT004";

    /// <summary>An <see cref="IMemberEmitter"/> threw while emitting a member.</summary>
    public const string EmitterFailed = "NPT005";

    /// <summary>No <see cref="INodeTranslator"/> is registered for a node's runtime type.</summary>
    public const string NoTranslatorForNode = "NPT006";

    /// <summary>An <see cref="IMemberEmitter"/> produced an invalid result: <c>DeclarePartial</c> set on
    /// a non-property member, or a modifier not allowed for the target.</summary>
    public const string InvalidEmitterOutput = "NPT007";
}
