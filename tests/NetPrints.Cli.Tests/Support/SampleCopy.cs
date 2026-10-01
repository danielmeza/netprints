using System.IO;
using NetPrints.Testing;

namespace NetPrints.Cli.Tests.Support;

/// <summary>A temporary copy of <c>samples/HelloWorld</c> wired to the repository's in-repo SDK, deleted on dispose.</summary>
internal sealed class SampleCopy : System.IDisposable
{
    public const string ProjectName = "HelloWorld.csproj";
    public const string GraphName = "HelloWorld.Program.netpc.json";
    public const string GeneratedName = "HelloWorld.Program.netpc.g.cs";

    public SampleCopy()
    {
        Directory = System.IO.Directory.CreateTempSubdirectory("np-sample-").FullName;
        string source = Path.Combine(LocalSdkLayout.FindRepositoryRoot(), "samples", "HelloWorld");
        foreach (string name in new[] { ProjectName, GraphName, GeneratedName })
        {
            File.Copy(Path.Combine(source, name), Path.Combine(Directory, name));
        }

        LocalSdkLayout.Write(Directory);
    }

    public string Directory { get; }

    public string Project => Path.Combine(Directory, ProjectName);

    public string Combine(string name) => Path.Combine(Directory, name);

    public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
}
