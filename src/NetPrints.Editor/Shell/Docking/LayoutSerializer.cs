using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetPrints.Editor.State;

namespace NetPrints.Editor.Shell.Docking;

/// <summary>Maps the persisted dock layout to and from the ADR-0018 envelope in <c>layout.json</c>.</summary>
internal static class LayoutSerializer
{
    /// <summary>The engine name written to the envelope.</summary>
    public const string Engine = "dock";

    /// <summary>Wraps a layout in the envelope.</summary>
    /// <param name="layout">The layout.</param>
    /// <returns>The state to save.</returns>
    public static LayoutState ToState(DockLayoutDto layout) =>
        new(StateFile.CurrentVersion, Engine, JsonSerializer.SerializeToElement(layout, DockJsonContext.Default.DockLayoutDto));

    /// <summary>Reads a layout from the envelope.</summary>
    /// <param name="state">The saved state, or null for none.</param>
    /// <param name="logger">Logs a layout that cannot be used.</param>
    /// <returns>The layout, or <see langword="null"/> for the default.</returns>
    public static DockLayoutDto? FromState(LayoutState? state, ILogger logger)
    {
        if (state is null)
        {
            return null;
        }

        if (!string.Equals(state.Engine, Engine, StringComparison.Ordinal))
        {
            Log.LayoutUnusable(logger, null, $"it is for the '{state.Engine}' layout engine");
            return null;
        }

        if (state.DockLayout is not { ValueKind: JsonValueKind.Object } element)
        {
            Log.LayoutUnusable(logger, null, "it has no layout tree");
            return null;
        }

        try
        {
            DockLayoutDto? layout = element.Deserialize(DockJsonContext.Default.DockLayoutDto);
            if (layout?.Root is null)
            {
                Log.LayoutUnusable(logger, null, "it has no main window layout");
                return null;
            }

            return layout;
        }
        catch (JsonException exception)
        {
            Log.LayoutUnusable(logger, exception, "it is malformed");
            return null;
        }
    }
}
