namespace NetPrints.Editor.StartPage;

/// <summary>A sample bundled with the editor.</summary>
/// <param name="Name">The sample's name, which is its folder's name.</param>
/// <param name="Directory">The bundled folder; never modified.</param>
/// <param name="ProjectFileName">The name of the <c>.csproj</c> in that folder.</param>
internal sealed record SampleDescriptor(string Name, string Directory, string ProjectFileName);
