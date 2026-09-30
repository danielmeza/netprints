using System;
using System.Diagnostics.CodeAnalysis;

namespace NetPrints.Catalog;

/// <summary>A catalog source could not be prepared: the restore failed or an assembly could not be read (the tool exits 1).</summary>
[Experimental(ExperimentalApis.CatalogProfiles, UrlFormat = ExperimentalApis.UrlFormat)]
public sealed class CatalogSourceException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">A description of the problem.</param>
    /// <param name="innerException">The underlying failure, when there is one.</param>
    public CatalogSourceException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
