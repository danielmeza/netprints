namespace NetPrints.Cli.Infrastructure;

/// <summary>The version of the running tool, as its assembly reports it (the generator it renders with is compiled into it).</summary>
/// <param name="Value">The informational version, possibly with build metadata after a <c>+</c>.</param>
internal sealed record ToolVersion(string Value);
