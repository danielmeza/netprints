using NetPrints.Serialization;

namespace NetPrints.Cli.Git;

/// <summary>Creates the merger the merge driver uses; tests replace it to inject a failure.</summary>
/// <param name="format">The document format of the merged graph.</param>
/// <returns>The merger.</returns>
internal delegate GraphMerger GraphMergerFactory(IDocumentFormat format);
