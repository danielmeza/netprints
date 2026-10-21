using System.IO.Compression;
using System.Xml.Linq;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Testing.Checks;

namespace NetPrints.Extensibility.Testing;

/// <summary>
/// Runs the checks that need no test code on an extension folder or an extension package: manifest (NPK001), packaging (NPK002),
/// type identity (NPK003) and API compatibility (NPK017). Nothing of the extension is loaded; its assemblies are read through metadata.
/// </summary>
public static class ExtensionVerifier
{
    private const string PackageTypeName = "NetPrintsExtension";
    private const string ExtensionFolderInPackage = "extension/";
    private const string NuspecExtension = ".nuspec";
    private const string ApiName = "API compatibility";
    private const string ApiRule = "every NetPrints type and member the extension references exists in the host";
    private const string PackagingRule = "the package is a NetPrintsExtension package that holds the extension folder under extension/";

    private static readonly string[] SuiteCodes = [NpkDiagnosticCodes.Manifest, NpkDiagnosticCodes.Packaging, NpkDiagnosticCodes.TypeIdentity];

    /// <summary>Verifies an extension folder.</summary>
    /// <param name="folder">The folder that holds <c>netprints-extension.json</c> and the extension's assemblies.</param>
    /// <param name="hostFolder">The folder of the host assemblies to check against; the assemblies this tool ships when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token to cancel the run.</param>
    /// <returns>The verification.</returns>
    public static Task<ExtensionVerification> VerifyFolderAsync(string folder, string? hostFolder, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        return VerifyAsync(folder, hostFolder, [], cancellationToken);
    }

    /// <summary>Verifies an extension package: a <c>.nupkg</c> of package type <c>NetPrintsExtension</c> that holds the extension folder under <c>extension/</c>.</summary>
    /// <param name="package">The package file.</param>
    /// <param name="hostFolder">The folder of the host assemblies to check against; the assemblies this tool ships when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token to cancel the run.</param>
    /// <returns>The verification.</returns>
    public static async Task<ExtensionVerification> VerifyPackageAsync(string package, string? hostFolder, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(package);
        string temporary = Directory.CreateTempSubdirectory("netprints-verify-").FullName;
        try
        {
            var problems = new List<ConformanceFinding>();
            await using (ZipArchive archive = await ZipFile.OpenReadAsync(package, cancellationToken).ConfigureAwait(false))
            {
                if (!HasPackageType(archive))
                {
                    problems.Add(new ConformanceFinding(NpkDiagnosticCodes.Packaging, PackagingRule, $"the package type {PackageTypeName} is not declared"));
                }

                if (!Extract(archive, temporary))
                {
                    problems.Add(new ConformanceFinding(NpkDiagnosticCodes.Packaging, PackagingRule, $"the package holds nothing under {ExtensionFolderInPackage}"));
                }
            }

            return await VerifyAsync(temporary, hostFolder, problems, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static async Task<ExtensionVerification> VerifyAsync(string folder, string? hostFolder, List<ConformanceFinding> packageProblems, CancellationToken cancellationToken)
    {
        var options = new ConformanceOptions { ExtensionFolder = folder };
        var results = new List<ConformanceCheckResult>();
        await using (ExtensionHarness harness = await ExtensionHarness.CreateAsync([], [], cancellationToken).ConfigureAwait(false))
        {
            foreach (string code in SuiteCodes)
            {
                ConformanceCheckResult result = await ConformanceSuite.RunCheckAsync(harness, options, code, cancellationToken).ConfigureAwait(false);
                results.Add(code == NpkDiagnosticCodes.Packaging && packageProblems.Count > 0 ? WithFindings(result, packageProblems) : result);
            }
        }

        string host = hostFolder ?? AppContext.BaseDirectory;
        using HostApi hostApi = HostApi.Open(host);
        (ConformanceCheckResult api, IReadOnlyList<MissingMember> missing) = CheckApi(folder, hostApi, cancellationToken);
        results.Add(api);

        ExtensionManifest? manifest = TryReadManifest(folder);
        return new ExtensionVerification(new ConformanceReport(results), manifest?.Id, manifest?.Version, manifest?.NetprintsApi, hostApi.AssemblyNames, missing);
    }

    private static (ConformanceCheckResult, IReadOnlyList<MissingMember>) CheckApi(string folder, HostApi host, CancellationToken cancellationToken)
    {
        ExtensionManifest? manifest = TryReadManifest(folder);
        string? path = manifest is null ? null : Path.Combine(folder, manifest.Assembly);
        if (path is null || !File.Exists(path))
        {
            string reason = $"the extension assembly cannot be read ({NpkDiagnosticCodes.Manifest}, {NpkDiagnosticCodes.Packaging})";
            return (new ConformanceCheckResult(NpkDiagnosticCodes.ApiCompatibility, ApiName, ConformanceStatus.Skipped, [], reason), []);
        }

        IReadOnlyList<MissingMember> missing = ApiCompatibilityScanner.Scan(path, host, cancellationToken);
        ConformanceFinding[] findings = [.. missing.Select(item => new ConformanceFinding(NpkDiagnosticCodes.ApiCompatibility, ApiRule, item.ToString()))];
        ConformanceStatus status = findings.Length == 0 ? ConformanceStatus.Passed : ConformanceStatus.Failed;
        return (new ConformanceCheckResult(NpkDiagnosticCodes.ApiCompatibility, ApiName, status, findings, string.Empty), missing);
    }

    private static ConformanceCheckResult WithFindings(ConformanceCheckResult result, List<ConformanceFinding> extra) =>
        new(result.Code, result.Name, ConformanceStatus.Failed, [.. extra, .. result.Findings], string.Empty);

    private static ExtensionManifest? TryReadManifest(string folder)
    {
        string path = Path.Combine(folder, ExtensionManifest.FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            return ExtensionManifest.Parse(stream, path);
        }
        catch (ExtensionManifestException)
        {
            return null;
        }
    }

    private static bool HasPackageType(ZipArchive archive)
    {
        ZipArchiveEntry? nuspec = archive.Entries.FirstOrDefault(entry => !entry.FullName.Contains('/', StringComparison.Ordinal) && entry.FullName.EndsWith(NuspecExtension, StringComparison.OrdinalIgnoreCase));
        if (nuspec is null)
        {
            return false;
        }

        using Stream stream = nuspec.Open();
        XDocument document = XDocument.Load(stream);
        return document.Descendants().Any(element => element.Name.LocalName == "packageType"
            && string.Equals((string?)element.Attribute("name"), PackageTypeName, StringComparison.Ordinal));
    }

    private static bool Extract(ZipArchive archive, string destination)
    {
        string root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        bool any = false;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (!entry.FullName.StartsWith(ExtensionFolderInPackage, StringComparison.Ordinal) || entry.Name.Length == 0)
            {
                continue;
            }

            string target = Path.GetFullPath(Path.Combine(destination, entry.FullName[ExtensionFolderInPackage.Length..]));
            if (!target.StartsWith(root, StringComparison.Ordinal))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? destination);
            entry.ExtractToFile(target, overwrite: true);
            any = true;
        }

        return any;
    }
}

/// <summary>A NetPrints type or member an extension references that the host does not have.</summary>
/// <param name="Assembly">The host assembly the reference points to.</param>
/// <param name="Type">The full name of the type; nested types are joined with <c>+</c>.</param>
/// <param name="Member">The member as <c>Name(ParameterTypes)</c> for a method or its name for a field, or <see langword="null"/> when the type itself is missing.</param>
/// <param name="ReferencedFrom">The extension type whose code references it.</param>
public sealed record MissingMember(string Assembly, string Type, string? Member, string ReferencedFrom)
{
    /// <summary>Formats the member as <c>assembly: type.member (referenced from type)</c>.</summary>
    /// <returns>The text.</returns>
    public override string ToString() => $"{Assembly}: {Type}{(Member is null ? string.Empty : "." + Member)} (referenced from {ReferencedFrom})";
}

/// <summary>What <see cref="ExtensionVerifier"/> found.</summary>
/// <param name="Report">One result per check, in code order: NPK001, NPK002, NPK003, NPK017.</param>
/// <param name="ExtensionId">The id in the manifest, or <see langword="null"/> when the manifest cannot be read.</param>
/// <param name="ExtensionVersion">The extension's version, or <see langword="null"/> when the manifest cannot be read.</param>
/// <param name="NetprintsApi">The <c>netprintsApi</c> the manifest asks for, or <see langword="null"/> when the manifest cannot be read.</param>
/// <param name="HostAssemblies">The simple names of the host's NetPrints assemblies.</param>
/// <param name="MissingMembers">The types and members the host lacks; empty unless NPK017 failed.</param>
public sealed record ExtensionVerification(
    ConformanceReport Report,
    string? ExtensionId,
    string? ExtensionVersion,
    string? NetprintsApi,
    IReadOnlyList<string> HostAssemblies,
    IReadOnlyList<MissingMember> MissingMembers);
