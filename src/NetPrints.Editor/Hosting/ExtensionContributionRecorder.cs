using NetPrints.Editor.Contributions;
using NetPrints.Extensibility.Editor;
using NetPrints.Extensibility.Loading;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Keeps what each loaded extension registered through <c>IExtensionBuilder.Editor</c> and places it in the contribution groups
/// (FR-032, Clarifications answer 2): the application-level kinds of an extension from the user directories join the app group once,
/// its project-level kinds join each session group, and every kind of an extension a project declares joins that project's session group.
/// When a project declares an extension the user directories already gave the app group, the session group hides the app group's items of that id, so the project's copy serves the session.
/// The host passes <see cref="Factory"/> in <see cref="ExtensionLoaderOptions.EditorContributions"/>.
/// </summary>
public sealed class ExtensionContributionRecorder
{
    private readonly object gate = new();
    private readonly Dictionary<string, Recording> recordings = new(StringComparer.Ordinal);
    private readonly HashSet<string> appliedToAppGroup = new(StringComparer.Ordinal);

    /// <summary>Gets the factory for <see cref="ExtensionLoaderOptions.EditorContributions"/>: it records the calls of the extension being loaded, replacing an earlier recording of the same id.</summary>
    public Func<ExtensionManifest, IEditorContributions> Factory => Record;

    /// <summary>Adds the application-level kinds of the extensions recorded so far to the app group of <paramref name="registry"/>, once per extension.</summary>
    /// <param name="registry">The registry to add to; call it after the built-ins registered and before the initial registration ends.</param>
    public void ApplyToAppGroup(ContributionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Recording[] pending;
        lock (gate)
        {
            pending = [.. recordings.Values.Where(recording => appliedToAppGroup.Add(recording.Manifest.Id))];
        }

        foreach (Recording recording in pending)
        {
            recording.ReplayTo(registry.For(recording.Manifest.Id, recording.Manifest.Name), includeApplicationLevel: true, includeProjectLevel: false);
        }
    }

    /// <summary>Fills the session group of one open project from the extensions that loaded with it.</summary>
    /// <param name="builder">The session group being added.</param>
    /// <param name="loaded">The registry in use for the project.</param>
    /// <param name="projectFolders">The extension folders the project declared; an extension loaded from one of them is the project's own.</param>
    public void PopulateSessionGroup(IContributionGroupBuilder builder, ExtensionRegistry loaded, IReadOnlyList<string> projectFolders)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(loaded);
        ArgumentNullException.ThrowIfNull(projectFolders);
        string[] folders = [.. projectFolders.Select(Path.GetFullPath)];
        foreach (ExtensionLoadResult.Loaded result in loaded.Results.OfType<ExtensionLoadResult.Loaded>())
        {
            Recording? recording;
            lock (gate)
            {
                recordings.TryGetValue(result.Id, out recording);
            }

            if (recording is null)
            {
                continue;
            }

            bool fromProject = result.ManifestPath is { } path
                && folders.Contains(Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty, StringComparer.Ordinal);
            bool applied;
            lock (gate)
            {
                applied = appliedToAppGroup.Contains(result.Id);
            }

            if (fromProject && applied)
            {
                builder.Shadow(result.Id);
            }

            recording.ReplayTo(builder.For(result.Id, result.Manifest.Name), includeApplicationLevel: fromProject, includeProjectLevel: true);
        }
    }

    private Recording Record(ExtensionManifest manifest)
    {
        var recording = new Recording(manifest);
        lock (gate)
        {
            recordings[manifest.Id] = recording;
        }

        return recording;
    }

    private sealed class Recording(ExtensionManifest manifest) : IEditorContributions
    {
        private readonly List<(bool ApplicationLevel, Action<IEditorContributions> Replay)> calls = [];

        public ExtensionManifest Manifest { get; } = manifest;

        public void ReplayTo(IEditorContributions target, bool includeApplicationLevel, bool includeProjectLevel)
        {
            foreach ((bool applicationLevel, Action<IEditorContributions> replay) in calls)
            {
                if (applicationLevel ? includeApplicationLevel : includeProjectLevel)
                {
                    replay(target);
                }
            }
        }

        public IEditorContributions AddCommand(CommandDescriptor command) => Add(command.Scope == CommandScope.Global, target => target.AddCommand(command));

        public IEditorContributions AddPanel(PanelDescriptor panel) => Add(false, target => target.AddPanel(panel));

        public IEditorContributions AddDashboardTile(DashboardTileDescriptor tile) => Add(true, target => target.AddDashboardTile(tile));

        public IEditorContributions AddContextMenuItem(ContextMenuItemDescriptor item) => Add(false, target => target.AddContextMenuItem(item));

        public IEditorContributions AddTooltipProvider(ITooltipProvider provider) => Add(false, target => target.AddTooltipProvider(provider));

        public IEditorContributions AddGoToProvider(IGoToProvider provider) => Add(false, target => target.AddGoToProvider(provider));

        public IEditorContributions AddStatusBarItem(StatusBarItemDescriptor item) => Add(false, target => target.AddStatusBarItem(item));

        public IEditorContributions AddDialog(DialogDescriptor dialog) => Add(true, target => target.AddDialog(dialog));

        public IEditorContributions AddSettingsPage(SettingsPageDescriptor page) => Add(true, target => target.AddSettingsPage(page));

        public IEditorContributions AddViewResources(string resourceUri) => Add(true, target => target.AddViewResources(resourceUri));

        private Recording Add(bool applicationLevel, Action<IEditorContributions> replay)
        {
            calls.Add((applicationLevel, replay));
            return this;
        }
    }
}
