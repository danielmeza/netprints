using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;
using NetPrints.Core;

namespace NetPrints.Extensibility.Settings;

/// <summary>
/// Names one extension's settings section and its value type (extension-points.md §7). An extension registers
/// at most one descriptor, and its <see cref="ExtensionId"/> is its manifest id.
/// </summary>
/// <param name="ExtensionId">The extension id; the key of the section in the settings file.</param>
/// <param name="ValueType">The CLR type of the section's value.</param>
[Experimental(ExperimentalApiIds.Settings, UrlFormat = ExperimentalApiIds.UrlFormat)]
public abstract record ExtensionSettingsDescriptor(string ExtensionId, Type ValueType);

/// <summary>
/// A settings section of value type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The section's value type.</typeparam>
/// <param name="ExtensionId">The extension id; the key of the section in the settings file.</param>
/// <param name="TypeInfo">Source-generated JSON metadata for <typeparamref name="T"/>.</param>
/// <param name="Default">The value used when the section is missing or invalid.</param>
[Experimental(ExperimentalApiIds.Settings, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed record ExtensionSettingsDescriptor<T>(string ExtensionId, JsonTypeInfo<T> TypeInfo, T Default)
    : ExtensionSettingsDescriptor(ExtensionId, typeof(T));
