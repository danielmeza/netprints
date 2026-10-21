using System.Globalization;

namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>NPK001: the manifest is valid and asks for an API version the host implements.</summary>
internal sealed class ManifestCheck : ConformanceCheck
{
    private const string ValidRule = "the manifest is valid";
    private const string ApiRule = "netprintsApi is an API version the host implements";

    public override string Code => NpkDiagnosticCodes.Manifest;

    public override string Name => "Manifest";

    public override Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        if (context.Options.ExtensionFolder is null)
        {
            return Task.FromResult(CheckOutcome.Skip("no ExtensionFolder"));
        }

        if (!context.TryReadManifest(out Loading.ExtensionManifest? manifest, out string? problem) || manifest is null)
        {
            return Task.FromResult(CheckOutcome.FromFindings([Finding(ValidRule, problem ?? "the manifest cannot be read")]));
        }

        Version host = ExtensionApi.Version;
        if (manifest.ApiVersion is not { } api || api.Major != host.Major || api.Minor > host.Minor)
        {
            string hostText = string.Create(CultureInfo.InvariantCulture, $"{host.Major}.{host.Minor}");
            return Task.FromResult(CheckOutcome.FromFindings([Finding(ApiRule, $"netprintsApi {manifest.NetprintsApi} (the host implements {hostText})")]));
        }

        return Task.FromResult(CheckOutcome.Pass);
    }
}
