using Dock.Model;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Serializer.SystemTextJson;

namespace NetPrints.Editor.Shell.Docking.Spike;

/// <summary>Saves a spike layout with Dock's System.Text.Json serializer and loads it back through the app.</summary>
internal sealed class SpikeLayoutStore
{
    private readonly IDockSerializer serializer = new DockSerializer();
    private readonly DockState state = new();

    /// <summary>Writes <paramref name="layout"/> to <paramref name="stream"/>.</summary>
    /// <param name="stream">The destination.</param>
    /// <param name="layout">The layout to save.</param>
    public void Save(Stream stream, IRootDock layout)
    {
        state.Save(layout);
        serializer.Save(stream, layout);
    }

    /// <summary>Reads a layout and re-attaches its documents through <paramref name="resolve"/>.</summary>
    /// <param name="stream">The source.</param>
    /// <param name="factory">The factory that initialises the loaded layout.</param>
    /// <param name="resolve">Maps a dockable id to its view model, or null when it no longer exists.</param>
    /// <returns>The layout, or null when the stream holds none.</returns>
    public IRootDock? Load(Stream stream, SpikeDockFactory factory, Func<string, object?> resolve)
    {
        if (serializer.Load<IRootDock?>(stream) is not { } layout)
        {
            return null;
        }

        state.Restore(layout);
        factory.Reattach(layout, resolve);
        factory.InitLayout(layout);
        return layout;
    }
}
