using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Settings;

/// <summary>
/// A settings store that tells its listeners when a section's stored value changed. The host's <c>IOptionsMonitor&lt;T&gt;</c>
/// of an extension section subscribes through it.
/// </summary>
[Experimental(ExperimentalApiIds.Settings, UrlFormat = ExperimentalApiIds.UrlFormat)]
public interface ISettingsChangeSource
{
    /// <summary>
    /// Subscribes to the changes of one section.
    /// </summary>
    /// <param name="sectionId">The section's id: <see cref="NetPrintsSettings.SectionId"/> or an extension id.</param>
    /// <param name="listener">Called after a write of the section has persisted; never called for a failed write.</param>
    /// <returns>A handle that ends the subscription when disposed.</returns>
    IDisposable OnSectionChanged(string sectionId, Action listener);
}
