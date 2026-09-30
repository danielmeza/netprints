namespace NetPrints.Cli;

/// <summary>The process exit codes of the <c>netprints</c> tool (contracts/cli.md §2, ADR-0015).</summary>
internal static class ExitCodes
{
    /// <summary>The command did what it was asked.</summary>
    public const int Success = 0;

    /// <summary>Build or generation errors, <c>--check</c> differences, an unreadable or invalid input file.</summary>
    public const int Failed = 1;

    /// <summary>An unknown command or option, an invalid value, a bad path argument or a removed P1 flag.</summary>
    public const int Usage = 2;

    /// <summary>No compatible .NET SDK is registered.</summary>
    public const int NoSdk = 3;

    /// <summary>An unhandled exception.</summary>
    public const int InternalError = 4;
}
