using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Extensibility.Loading;

/// <summary>
/// Builds the provider of one extension from its <see cref="IExtensionBuilder.Services"/> and the <see cref="HostExports"/>, after the seam filter
/// removed the registrations that replace or configure host services (research R15).
/// </summary>
internal static class ExtensionServiceProviders
{
    private const string HostAssemblyPrefix = "NetPrints.";

    private static readonly Type[] OptionsSeams = [typeof(IConfigureOptions<>), typeof(IPostConfigureOptions<>), typeof(IValidateOptions<>)];

    private static readonly Type[] ReplacedExports = [typeof(ILoggerFactory), typeof(TimeProvider)];

    private static readonly Type[] ReplacedOpenExports = [typeof(ILogger<>), typeof(IOptionsMonitor<>)];

    /// <summary>Builds the provider.</summary>
    /// <param name="services">The extension's registrations.</param>
    /// <param name="ownAssembly">The extension's assembly: a type declared in it is the extension's own.</param>
    /// <param name="sections">The settings sections the extension declared.</param>
    /// <param name="settingsStore">The store behind the sections, or <see langword="null"/> to read their defaults.</param>
    /// <param name="loggerFactory">The host's logger factory.</param>
    /// <param name="reject">Called for each removed registration with the service type text and the reason.</param>
    /// <returns>The provider.</returns>
    [RequiresUnreferencedCode(DynamicLoading.Reason)]
    public static ServiceProvider Build(
        IServiceCollection services,
        Assembly ownAssembly,
        IReadOnlyList<ExtensionSettingsDescriptor> sections,
        ISettingsStore? settingsStore,
        ILoggerFactory loggerFactory,
        Action<string, string> reject)
    {
        IServiceCollection accepted = new ServiceCollection();
        foreach (ServiceDescriptor descriptor in services)
        {
            if (Violation(descriptor.ServiceType, ownAssembly) is { } reason)
            {
                reject($"service {descriptor.ServiceType}", reason);
            }
            else
            {
                accepted.Add(descriptor);
            }
        }

        accepted.AddSingleton(loggerFactory);
        accepted.AddSingleton(TimeProvider.System);
        accepted.AddSingleton(typeof(ILogger<>), typeof(ExtensionLogger<>));
        foreach (ExtensionSettingsDescriptor section in sections)
        {
            accepted.AddSingleton(
                typeof(IOptionsMonitor<>).MakeGenericType(section.ValueType),
                _ => Activator.CreateInstance(typeof(SettingsOptionsMonitor<>).MakeGenericType(section.ValueType), section, settingsStore)
                    ?? throw new InvalidOperationException("The options monitor could not be created."));
        }

        return accepted.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static string? Violation(Type serviceType, Assembly ownAssembly)
    {
        if (Array.IndexOf(ReplacedExports, serviceType) >= 0
            || (serviceType.IsGenericTypeDefinition && Array.IndexOf(ReplacedOpenExports, serviceType) >= 0))
        {
            return "it replaces a host export.";
        }

        if (serviceType.IsGenericType && !serviceType.IsGenericTypeDefinition
            && Array.IndexOf(OptionsSeams, serviceType.GetGenericTypeDefinition()) >= 0
            && serviceType.GetGenericArguments().Any(argument => !IsOwn(argument, ownAssembly)))
        {
            return "it configures options the extension does not own.";
        }

        return IsHostType(serviceType, ownAssembly) ? "it is a closed generic over a host type, or a host type itself." : null;
    }

    private static bool IsHostType(Type type, Assembly ownAssembly)
    {
        if (type.IsGenericParameter || type.IsGenericTypeDefinition)
        {
            return false;
        }

        if (type.HasElementType)
        {
            return type.GetElementType() is { } element && IsHostType(element, ownAssembly);
        }

        bool host = !IsOwn(type, ownAssembly) && (type.Assembly.GetName().Name?.StartsWith(HostAssemblyPrefix, StringComparison.Ordinal) ?? false);
        return host || (type.IsGenericType && type.GetGenericArguments().Any(argument => IsHostType(argument, ownAssembly)));
    }

    private static bool IsOwn(Type type, Assembly ownAssembly) => type.Assembly == ownAssembly;
}

/// <summary>An <see cref="ILogger{TCategoryName}"/> over the host's factory, so an extension asks for the logger of its own types.</summary>
/// <typeparam name="T">The category type.</typeparam>
/// <param name="factory">The host's logger factory.</param>
internal sealed class ExtensionLogger<T>(ILoggerFactory factory) : ILogger<T>
{
    private readonly ILogger inner = factory.CreateLogger(typeof(T).FullName ?? typeof(T).Name);

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => inner.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        inner.Log(logLevel, eventId, state, exception, formatter);
}

/// <summary>The <see cref="IOptionsMonitor{TOptions}"/> of one settings section: the stored value, or the section's default without a store.</summary>
/// <typeparam name="T">The section's value type.</typeparam>
/// <param name="section">The section.</param>
/// <param name="store">The store, or <see langword="null"/>.</param>
[RequiresUnreferencedCode(DynamicLoading.Reason)]
internal sealed class SettingsOptionsMonitor<T>(ExtensionSettingsDescriptor<T> section, ISettingsStore? store) : IOptionsMonitor<T>
{
    public T CurrentValue => store is null ? section.Default : store.Get(section);

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => NoChanges.Instance;

    private sealed class NoChanges : IDisposable
    {
        public static NoChanges Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
