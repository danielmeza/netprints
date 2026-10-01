using Dock.Model.Mvvm.Controls;

namespace NetPrints.Editor.Shell.Docking.Spike;

/// <summary>A spike document dockable; its <c>Context</c> is the NetPrints view model, re-attached by id after a load.</summary>
public sealed class SpikeDocument : Document
{
}

/// <summary>A spike tool dockable; its <c>Context</c> is the NetPrints view model.</summary>
public sealed class SpikeTool : Tool
{
}
