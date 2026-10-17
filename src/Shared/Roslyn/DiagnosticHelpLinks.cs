namespace NetPrints.Roslyn
{
    /// <summary>Help links of the NetPrints analyzers: every code has an anchor on the diagnostics guide page.</summary>
    internal static class DiagnosticHelpLinks
    {
        /// <summary>The diagnostics guide page that lists every NetPrints diagnostic code.</summary>
        public const string DiagnosticsGuide = "https://danielmeza.github.io/netprints/guide/diagnostics";

        /// <summary>The help link of a diagnostic code, such as <c>NPS001</c>.</summary>
        /// <param name="code">The diagnostic code.</param>
        /// <returns>The guide URL with the lower-case code as its anchor.</returns>
        public static string For(string code) => DiagnosticsGuide + "#" + code.ToLowerInvariant();
    }
}
