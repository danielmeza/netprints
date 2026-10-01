using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Serialization;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;

namespace NetPrints.Cli.Infrastructure;

/// <summary>The document formats <c>format</c> and <c>show</c> read graphs with: the built-in JSON format, with no extension loaded.</summary>
internal static class GraphFormats
{
    /// <summary>Creates the registry. A node of an extension kind reads as an unknown node and is written back unchanged.</summary>
    /// <returns>A registry holding the built-in JSON format.</returns>
    public static DocumentFormatRegistry CreateRegistry() =>
        new([
            new JsonDocumentFormat(
                new NetPrintsJsonOptions(new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, [])),
                new DocumentMigrator([], NullLogger<DocumentMigrator>.Instance)),
        ]);
}
