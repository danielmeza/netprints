using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Settings;

/// <summary>
/// A settings section the store replaced by its defaults because it failed validation.
/// </summary>
/// <param name="SectionId">The section's id: a host section such as <c>run</c>, or an extension id.</param>
/// <param name="Code">The <see cref="NetPrints.Extensibility.Loading.ExtensionDiagnosticCodes.SettingsInvalid"/> code for an extension's section; <see langword="null"/> for a section of the editor or the host.</param>
/// <param name="Reason">The validator's messages, each naming its key.</param>
[Experimental(ExperimentalApiIds.Settings, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed record SettingsSectionIssue(string SectionId, string? Code, string Reason);
