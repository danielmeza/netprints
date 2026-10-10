namespace NetPrints.Editor.StartPage;

/// <summary>The <c>Program</c> class graph an Executable template seeds: an empty <c>public static void Main()</c>, so the project has an entry point.</summary>
internal static class ProgramGraphSeed
{
    /// <summary>The file the seed is written to, in the project's folder.</summary>
    public const string FileName = "Program.netpc.json";

    private const string NamespaceToken = "{Namespace}";

    private const string Template = """
        {
          "$schema": "https://danielmeza.github.io/netprints/schemas/netpc.v1.schema.json",
          "schemaVersion": 1,
          "namespace": "{Namespace}",
          "name": "Program",
          "visibility": "Public",
          "classGraph": {
            "nodes": [
              { "$kind": "classReturn", "id": "n000000000vny0" }
            ]
          },
          "methods": [
            {
              "id": "m000000001gs20",
              "name": "Main",
              "visibility": "Public",
              "modifiers": "Static",
              "graph": {
                "nodes": [
                  { "$kind": "methodEntry", "id": "n000000000vny1" },
                  { "$kind": "return", "id": "n000000000vny2" }
                ],
                "connections": [
                  { "from": "n000000000vny1/out.exec.Exec", "to": "n000000000vny2/in.exec.Exec" }
                ]
              }
            }
          ],
          "layout": {
            "class": {
              "n000000000vny0": [112, 112]
            },
            "m000000001gs20": {
              "n000000000vny1": [112, 112],
              "n000000000vny2": [420, 112]
            }
          }
        }

        """;

    /// <summary>Renders the graph file's text, with LF endings.</summary>
    /// <param name="rootNamespace">The project's root namespace; a validated C# namespace, so it needs no escaping.</param>
    /// <returns>The JSON text.</returns>
    public static string Render(string rootNamespace) =>
        Template.Replace(NamespaceToken, rootNamespace, StringComparison.Ordinal).ReplaceLineEndings("\n");
}
