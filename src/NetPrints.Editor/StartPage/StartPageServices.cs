namespace NetPrints.Editor.StartPage;

/// <summary>The services the dashboard tiles ask for through <c>DashboardTileDescriptor.CreateViewModel</c>.</summary>
internal sealed class StartPageServices : IServiceProvider
{
    private readonly Dictionary<Type, object> services = [];

    /// <summary>Registers the service of type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="service">The service.</param>
    /// <returns>This instance.</returns>
    public StartPageServices Add<T>(T service)
        where T : class
    {
        services[typeof(T)] = service;
        return this;
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType) => services.GetValueOrDefault(serviceType);

    /// <summary>Gets a registered service.</summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="provider">The provider.</param>
    /// <returns>The service.</returns>
    /// <exception cref="InvalidOperationException">Nothing is registered for <typeparamref name="T"/>.</exception>
    public static T Require<T>(IServiceProvider provider)
        where T : class =>
        provider.GetService(typeof(T)) as T ?? throw new InvalidOperationException($"The start page needs a {typeof(T).Name}.");
}
