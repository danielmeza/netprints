using NetPrints.Editor.Contributions;

namespace NetPrints.Editor.Hosting;

/// <summary>A view resource declaration the loader could not use.</summary>
/// <param name="Declaration">The declaration that was ignored.</param>
/// <param name="Reason">Why it could not be used.</param>
public sealed record ViewResourceFailure(ViewResourceDeclaration Declaration, string Reason);
