using System.Text.Json.Serialization;

namespace NetPrints.Workspace;

/// <summary>
/// Narrow, deliberately minimal shape behind <see cref="NetPrints.Projects.ProjectSnapshot.CompilationOptionsJson"/>
/// ("serialized language version, nullable, usings" per project-system.md §4's own comment): the
/// full contract for what <c>CodeAnalysisSession</c> (compilation-and-diagnostics.md §2, T089/T092+)
/// needs is not specified beyond that phrase, so this is a reasonable, revisitable placeholder —
/// see implementation-notes.md.
/// </summary>
internal sealed record CompilationOptionsInfo(string LanguageVersion, string Nullable, bool ImplicitUsings);

/// <summary>Source-generated serializer metadata for <see cref="CompilationOptionsInfo"/>.</summary>
[JsonSerializable(typeof(CompilationOptionsInfo))]
internal sealed partial class CompilationOptionsJsonContext : JsonSerializerContext;
