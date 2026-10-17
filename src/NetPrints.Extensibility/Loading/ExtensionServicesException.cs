namespace NetPrints.Extensibility.Loading;

/// <summary>The service registrations of an extension do not form a resolvable provider: the extension fails at load.</summary>
internal sealed class ExtensionServicesException : Exception
{
    public ExtensionServicesException()
    {
    }

    public ExtensionServicesException(string message)
        : base(message)
    {
    }

    public ExtensionServicesException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
