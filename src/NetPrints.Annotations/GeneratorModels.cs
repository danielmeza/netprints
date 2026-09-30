using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NetPrints.Annotations
{
    /// <summary>A source location as plain data, so diagnostics can travel through cached pipeline steps.</summary>
    internal sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
    {
        public static LocationInfo? From(Location location) =>
            location.SourceTree is null ? null : new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);

        public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);
    }

    /// <summary>One <c>[assembly: NetPrintsCatalog(...)]</c> attribute.</summary>
    internal sealed record CatalogRequest(
        string AssemblyName,
        string? Id,
        string? Profile,
        EquatableArray<string> Include,
        EquatableArray<string> Exclude,
        string? AccessorName,
        LocationInfo? Location);

    /// <summary>
    /// An additional file the generator reads: a <c>*.npprofile.json</c> profile (its text is kept) or a reference documentation file (only the
    /// <see cref="AdditionalText"/> is kept, and its text is read when a request needs it).
    /// </summary>
    internal sealed record AdditionalFileModel(string Path, string FileName, string? Text, AdditionalText? Source, bool IsProfile, bool IsDocumentation);

    /// <summary>Everything a referenced-assembly build reads besides the request.</summary>
    internal sealed record BuildInputs(
        EquatableArray<MetadataReference> References,
        EquatableArray<AdditionalFileModel> Files,
        string? RootNamespace);

    /// <summary>A generator diagnostic as plain data.</summary>
    internal sealed record DiagnosticModel(string Code, bool IsError, string Message, LocationInfo? Location);

    /// <summary>The outcome for one catalog: the file to add, or only diagnostics when it failed.</summary>
    internal sealed record CatalogOutput(string? Id, string? HintName, string? Source, string? Accessor, EquatableArray<DiagnosticModel> Diagnostics)
    {
        public bool Succeeded => Source is not null;

        public static CatalogOutput Failed(params DiagnosticModel[] diagnostics) => new CatalogOutput(null, null, null, null, new EquatableArray<DiagnosticModel>(diagnostics));
    }
}
