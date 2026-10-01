using Microsoft.Extensions.Logging;

namespace NetPrints.Catalog;

/// <summary>Source-generated log messages for the catalog runtime.</summary>
internal static partial class Log
{
    /// <summary>Logs 5001 (NPC103): a catalog whose id is already loaded was ignored.</summary>
    [LoggerMessage(EventId = 5001, Level = LogLevel.Warning, Message = "NPC103: catalog '{Id}' version {Version} ignored: a catalog with this id is already loaded (the first in registry order wins)")]
    public static partial void DuplicateCatalogId(ILogger logger, string id, string version);
}
