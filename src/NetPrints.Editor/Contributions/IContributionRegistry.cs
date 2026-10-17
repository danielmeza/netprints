namespace NetPrints.Editor.Contributions;

/// <summary>
/// The single registry of everything the editor's surfaces are generated from (ADR-0020). It keeps an app group
/// (built-ins first, then per-user extensions in load order) and one session group per open project. Every <c>Add*</c>
/// validates and records the owner. Rejected or altered contributions are listed in <see cref="IContributionSnapshot.Issues"/>;
/// within a kind the first registration wins. Every change publishes a new immutable <see cref="ContributionSnapshot"/>
/// and raises <see cref="Changed"/>; the registry's own lists always read the current snapshot.
/// </summary>
public interface IContributionRegistry : IContributionSnapshot
{
    /// <summary>Raised after the contributions changed, with the new snapshot; a surface rebuilds from it.</summary>
    event EventHandler<ContributionSnapshot>? Changed;

    /// <summary>Gets the current snapshot.</summary>
    ContributionSnapshot Snapshot { get; }

    /// <summary>Adds a project template to the app group; an invalid or duplicate one is ignored.</summary>
    /// <param name="template">The template.</param>
    void AddProjectTemplate(ProjectTemplateDescriptor template);

    /// <summary>Adds the session group of one open project: <paramref name="populate"/> fills it and it is published as a whole.</summary>
    /// <param name="key">Identifies the group, normally the project's; it must not be in use.</param>
    /// <param name="populate">Fills the group. When it throws, nothing is published and the exception propagates.</param>
    /// <exception cref="InvalidOperationException">A session group with this key exists.</exception>
    void AddSessionGroup(string key, Action<IContributionGroupBuilder> populate);

    /// <summary>Removes a session group as a whole.</summary>
    /// <param name="key">The key the group was added with.</param>
    /// <returns><see langword="true"/> when the group existed.</returns>
    bool RemoveSessionGroup(string key);

    /// <summary>Ends the initial registration of the app group: adds the Show-panel command of every panel without one and publishes the first snapshot. Later additions are still accepted.</summary>
    void Freeze();
}
