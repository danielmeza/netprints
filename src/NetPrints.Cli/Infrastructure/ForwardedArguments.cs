using System.Collections.Generic;

namespace NetPrints.Cli.Infrastructure;

/// <summary>
/// The arguments after the first <c>--</c> of the command line, cut off before Spectre tokenizes them so that empty strings, a lone <c>-</c>
/// and option-like values reach <c>netprints run</c>'s program unchanged.
/// </summary>
/// <param name="Values">The arguments, in order, without the separator.</param>
internal sealed record ForwardedArguments(IReadOnlyList<string> Values);
