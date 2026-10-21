using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>NPK003: every NetPrints assembly the extension references is one the host shares with it, so contracts resolve to the host's types.</summary>
internal sealed class TypeIdentityCheck : ConformanceCheck
{
    private const string Rule = "the extension references the host's assemblies for NetPrints types, not private copies";

    public override string Code => NpkDiagnosticCodes.TypeIdentity;

    public override string Name => "Type identity";

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

        string path = Path.Combine(folder, manifest.Assembly);
        if (!File.Exists(path))
        {
            return Task.FromResult(CheckOutcome.Skip($"the assembly is missing ({NpkDiagnosticCodes.Packaging})"));
        }

        var findings = new List<ConformanceFinding>();
        using FileStream stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        MetadataReader reader = pe.GetMetadataReader();
        foreach (AssemblyReferenceHandle handle in reader.AssemblyReferences)
        {
            string name = reader.GetString(reader.GetAssemblyReference(handle).Name);
            if (HostAssemblyNames.IsNetPrints(name) && !HostAssemblyNames.Contract.Contains(name))
            {
                findings.Add(Finding(Rule, $"{manifest.Assembly} references {name}, which is not part of the extension contract"));
            }
        }

        return Task.FromResult(CheckOutcome.FromFindings(findings));
    }
}
