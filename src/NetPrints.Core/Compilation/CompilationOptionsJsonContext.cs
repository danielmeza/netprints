using System.Text.Json.Serialization;

namespace NetPrints.Compilation;

/// <summary>
/// Narrow, deliberately minimal shape behind <see cref="NetPrints.Projects.ProjectSnapshot.CompilationOptionsJson"/>,
/// matching <c>NetPrints.Workspace.MsBuildProjectSystem</c>'s <c>CompilationOptionsInfo</c>
/// (same property names; the shape is not otherwise part of the contract, so keeping two independent
/// copies is deliberate rather than sharing a public type across the projects/analysis boundary).
/// </summary>
internal sealed record CompilationOptionsInfo(string LanguageVersion, string Nullable, bool ImplicitUsings);

/// <summary>Source-generated serializer metadata for <see cref="CompilationOptionsInfo"/>.</summary>
[JsonSerializable(typeof(CompilationOptionsInfo))]
internal sealed partial class CompilationOptionsJsonContext : JsonSerializerContext;
