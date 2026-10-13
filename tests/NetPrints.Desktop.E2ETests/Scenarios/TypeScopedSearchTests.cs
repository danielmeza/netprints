using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Testing;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>In a project that references the annotated library, the search from a pin of a cataloged type lists only the catalog's members (FR-090, US10).</summary>
public sealed class TypeScopedSearchTests(DesktopWorkerPool pool) : ProjectEditorTestBase(pool)
{
    private const string MainMethod = "Main";
    private const string ConstructorNode = "ConstructorNode";
    private const string GreeterPin = "Greeter";
    private const string CatalogMember = "Count";
    private const string HiddenCatalogMember = "Undeclared";
    private const string GreeterType = "AnnotatedFixture.Greeter";

    private const string ClassGraph = """
        {
          "$schema": "https://danielmeza.github.io/netprints/schemas/netpc.v1.schema.json",
          "schemaVersion": 1,
          "namespace": "HelloWorld",
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
                  { "$kind": "return", "id": "n000000000vny2" },
                  {
                    "$kind": "constructor",
                    "id": "n000000000vny3",
                    "constructor": {
                      "declaringType": { "name": "AnnotatedFixture.Greeter" },
                      "parameters": []
                    }
                  }
                ],
                "connections": [
                  { "from": "n000000000vny1/out.exec.Exec", "to": "n000000000vny3/in.exec.Exec" },
                  { "from": "n000000000vny3/out.exec.Exec", "to": "n000000000vny2/in.exec.Exec" }
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
              "n000000000vny2": [840, 112],
              "n000000000vny3": [420, 112]
            }
          }
        }
        """;

    protected override EditorStart EditorStartFor(string sampleProject)
    {
        string directory = Path.GetDirectoryName(sampleProject) ?? throw new InvalidOperationException("The sample project has no folder.");
        File.WriteAllText(Path.Combine(directory, "HelloWorld.Program.netpc.json"), ClassGraph);
        File.WriteAllText(sampleProject, $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <RootNamespace>HelloWorld</RootNamespace>
                <NetPrintsProfile>netprints.default</NetPrintsProfile>
              </PropertyGroup>

              <ItemGroup>
                <Reference Include="CatalogAnnotatedLib">
                  <HintPath>{FixtureExtensions.AnnotatedLibraryAssembly()}</HintPath>
                </Reference>
                <PackageReference Include="NetPrints.Sdk" Version="0.1.0" Condition="'$(NetPrintsUseLocalSdk)' != 'true'" PrivateAssets="all" />
              </ItemGroup>

            </Project>

            """);
        return base.EditorStartFor(sampleProject);
    }

    [Fact]
    public Task SearchFromAPinOfACatalogedTypeListsOnlyTheCatalogMembers() => RunScenarioAsync(async token =>
    {
        await StartAsync(token);
        await WaitForProjectAsync(token);
        await Shell.OpenMethodAsync(MainMethod, token);

        using (Step("drag a cable from the Greeter pin onto empty canvas"))
        {
            await Shell.Graph.Node(ConstructorNode).Output(GreeterPin).DragCableToAsync(await Shell.Graph.EmptyPointAsync(token), token);
            await Shell.Graph.Search.WaitOpenAsync(token);
        }

        using (Step("only the catalog's members are listed"))
        {
            IReadOnlyList<string> rows = await Shell.Graph.Search.RowTextsAsync(token);
            string listed = string.Join(" | ", rows);
            Assert.True(rows.Any(row => row.Contains(CatalogMember, StringComparison.Ordinal)), $"The catalog member is missing: {listed}");
            Assert.False(rows.Any(row => row.Contains(HiddenCatalogMember, StringComparison.Ordinal)), $"A member outside the catalog is listed: {listed}");
            string[] greeterRows = [.. rows.Where(row => row.Contains(GreeterType, StringComparison.Ordinal))];
            Assert.Single(greeterRows);
            Assert.Contains(CatalogMember, greeterRows[0], StringComparison.Ordinal);
        }
    });
}
