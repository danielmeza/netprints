namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>NPK002: the extension folder has the loader's layout and holds no copy of a host assembly.</summary>
internal sealed class PackagingCheck : ConformanceCheck
{
    private const string AssemblyRule = "the folder holds the assembly the manifest names";
    private const string CopyRule = "the folder holds no copy of a host assembly";
    private const string AssemblyExtension = ".dll";

    public override string Code => NpkDiagnosticCodes.Packaging;

    public override string Name => "Packaging";

    public override Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        string? folder = context.Options.ExtensionFolder;
        if (folder is null)
        {
            return Task.FromResult(CheckOutcome.Skip("no ExtensionFolder"));
        }

        if (!context.TryReadManifest(out Loading.ExtensionManifest? manifest, out string? problem) || manifest is null)
        {
            return Task.FromResult(CheckOutcome.Skip($"the manifest cannot be read ({NpkDiagnosticCodes.Manifest}): {problem}"));
        }

        var findings = new List<ConformanceFinding>();
        if (!File.Exists(Path.Combine(folder, manifest.Assembly)))
        {
            findings.Add(Finding(AssemblyRule, manifest.Assembly));
        }

        string own = Path.GetFileNameWithoutExtension(manifest.Assembly);
        foreach (string file in Directory.EnumerateFiles(folder, "*" + AssemblyExtension, SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (!string.Equals(name, own, StringComparison.Ordinal) && HostAssemblyNames.IsProvidedByHost(name))
            {
                findings.Add(Finding(CopyRule, Path.GetRelativePath(folder, file)));
            }
        }

        return Task.FromResult(CheckOutcome.FromFindings(findings));
    }
}
