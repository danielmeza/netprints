using System;

namespace NetPrints.Catalog;

/// <summary>A configuration file or the merged settings are invalid (the tool exits 1).</summary>
public sealed class CatalogConfigException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">A description of the problem.</param>
    /// <param name="innerException">The underlying failure, when there is one.</param>
    public CatalogConfigException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
