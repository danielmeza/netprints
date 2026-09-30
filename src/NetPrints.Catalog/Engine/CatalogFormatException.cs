using System;

namespace NetPrints.Catalog;

/// <summary>A catalog file could not be read: unsupported schema version (NPC101) or malformed content (NPC102).</summary>
public sealed class CatalogFormatException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="code">A <see cref="CatalogDiagnosticCodes"/> value.</param>
    /// <param name="message">A description of the problem.</param>
    /// <param name="innerException">The underlying failure, when there is one.</param>
    public CatalogFormatException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>The diagnostic code, one of <see cref="CatalogDiagnosticCodes"/>.</summary>
    public string Code { get; }
}
