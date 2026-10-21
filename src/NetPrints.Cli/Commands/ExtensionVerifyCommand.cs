using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Cli.Infrastructure;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Testing;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Commands;

/// <summary>
/// <c>netprints extension verify &lt;folder|package&gt;</c>: runs the checks that need no test code (NPK001 manifest, NPK002 packaging, NPK003 type identity,
/// NPK017 API compatibility) on an extension folder or package, reading its assemblies through metadata and loading nothing (FR-080).
/// </summary>
internal sealed class ExtensionVerifyCommand(IAnsiConsole console, CliEnvironment environment) : AsyncCommand<ExtensionVerifySettings>
{
    /// <summary>The command name.</summary>
    public const string Name = "verify";

    /// <summary>The branch the command lives under.</summary>
    public const string Branch = "extension";

    /// <summary>The plain-text report format.</summary>
    public const string TextFormat = "text";

    /// <summary>The JSON report format.</summary>
    public const string JsonFormat = "json";

    private const string PackageExtension = ".nupkg";
    private const int SchemaVersion = 1;
    private const string PassedText = "passed";

    /// <inheritdoc/>
    public override async Task<int> ExecuteAsync(CommandContext context, ExtensionVerifySettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.Format is not (TextFormat or JsonFormat))
        {
            await environment.Error.WriteLineAsync($"Unknown --format '{settings.Format}': use {TextFormat} or {JsonFormat}.").ConfigureAwait(false);
            return ExitCodes.Usage;
        }

        string path = Path.GetFullPath(settings.Path, environment.CurrentDirectory);
        bool isFolder = Directory.Exists(path);
        bool isPackage = !isFolder && File.Exists(path) && string.Equals(Path.GetExtension(path), PackageExtension, StringComparison.OrdinalIgnoreCase);
        if (!isFolder && !isPackage)
        {
            await environment.Error.WriteLineAsync($"'{path}' does not exist or is neither an extension folder nor a {PackageExtension} package.").ConfigureAwait(false);
            return ExitCodes.Usage;
        }

        string? host = settings.Host is null ? null : Path.GetFullPath(settings.Host, environment.CurrentDirectory);
        if (host is not null && !Directory.Exists(host))
        {
            await environment.Error.WriteLineAsync($"The --host folder '{host}' does not exist.").ConfigureAwait(false);
            return ExitCodes.Usage;
        }

        ExtensionVerification verification;
        try
        {
            verification = isFolder
                ? await ExtensionVerifier.VerifyFolderAsync(path, host, cancellationToken).ConfigureAwait(false)
                : await ExtensionVerifier.VerifyPackageAsync(path, host, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            await environment.Error.WriteLineAsync($"'{path}' cannot be read: {ex.Message}").ConfigureAwait(false);
            return ExitCodes.Usage;
        }

        string report = settings.Format == JsonFormat ? Json(verification) : Text(verification);
        await console.Profile.Out.Writer.WriteAsync(report).ConfigureAwait(false);
        return verification.Report.Passed ? ExitCodes.Success : ExitCodes.Failed;
    }

    private static string Text(ExtensionVerification verification)
    {
        var text = new StringBuilder();
        foreach (ConformanceCheckResult result in verification.Report.Results)
        {
            text.Append(result.Code).Append(' ').Append(result.Name).Append(": ").Append(Status(result.Status));
            if (result.Note.Length > 0)
            {
                text.Append(" (").Append(result.Note).Append(')');
            }

            text.AppendLine();
            if (result.Code != NpkDiagnosticCodes.ApiCompatibility)
            {
                foreach (ConformanceFinding finding in result.Findings)
                {
                    text.Append("  ").Append(finding).AppendLine();
                }
            }
        }

        foreach (MissingMember missing in verification.MissingMembers)
        {
            text.Append("  ").Append(missing).AppendLine();
        }

        return text.ToString();
    }

    private static string Json(ExtensionVerification verification)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", SchemaVersion);
            writer.WriteStartObject("extension");
            writer.WriteString("id", verification.ExtensionId);
            writer.WriteString("version", verification.ExtensionVersion);
            writer.WriteString("netprintsApi", verification.NetprintsApi);
            writer.WriteEndObject();
            writer.WriteStartObject("host");
            writer.WriteString("apiVersion", string.Create(CultureInfo.InvariantCulture, $"{ExtensionApi.Version.Major}.{ExtensionApi.Version.Minor}"));
            writer.WriteStartArray("assemblies");
            foreach (string assembly in verification.HostAssemblies)
            {
                writer.WriteStringValue(assembly);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteStartArray("checks");
            foreach (ConformanceCheckResult result in verification.Report.Results)
            {
                WriteCheck(writer, result, verification.MissingMembers.Count);
            }

            writer.WriteEndArray();
            writer.WriteStartArray("missingMembers");
            foreach (MissingMember missing in verification.MissingMembers)
            {
                writer.WriteStartObject();
                writer.WriteString("assembly", missing.Assembly);
                writer.WriteString("type", missing.Type);
                writer.WriteString("member", missing.Member);
                writer.WriteString("referencedFrom", missing.ReferencedFrom);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + Environment.NewLine;
    }

    private static void WriteCheck(Utf8JsonWriter writer, ConformanceCheckResult result, int missingCount)
    {
        writer.WriteStartObject();
        writer.WriteString("id", result.Code);
        writer.WriteString("name", JsonName(result.Code));
        writer.WriteString("status", Status(result.Status));
        writer.WriteStartArray("messages");
        if (result.Code == NpkDiagnosticCodes.ApiCompatibility)
        {
            if (missingCount > 0)
            {
                writer.WriteStringValue(string.Create(CultureInfo.InvariantCulture, $"{missingCount} referenced types or members are missing from the host"));
            }
        }
        else
        {
            foreach (ConformanceFinding finding in result.Findings)
            {
                writer.WriteStringValue(finding.ToString());
            }
        }

        if (result.Status == ConformanceStatus.Skipped && result.Note.Length > 0)
        {
            writer.WriteStringValue(result.Note);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static string JsonName(string code) => code switch
    {
        NpkDiagnosticCodes.Manifest => "manifest",
        NpkDiagnosticCodes.Packaging => "packaging",
        NpkDiagnosticCodes.TypeIdentity => "typeIdentity",
        NpkDiagnosticCodes.ApiCompatibility => "apiCompatibility",
        _ => code,
    };

    private static string Status(ConformanceStatus status) => status switch
    {
        ConformanceStatus.Passed => PassedText,
        ConformanceStatus.Failed => "failed",
        _ => "skipped",
    };
}
