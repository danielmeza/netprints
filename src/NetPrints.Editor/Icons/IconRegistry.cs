using System.Collections.Concurrent;
using System.Collections.Frozen;
using Material.Icons;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace NetPrints.Editor.Icons;

/// <summary>A resolved vector glyph; the only type that carries the icon library's glyph kind.</summary>
/// <param name="Kind">The library glyph kind.</param>
public readonly record struct IconGlyph(MaterialIconKind Kind);

/// <summary>Resolves icon ids to Material Design Icons glyphs (ADR-0021 Amendment 1); the only code that names <see cref="MaterialIconKind"/>.</summary>
/// <param name="logger">Receives one warning per unknown id.</param>
public sealed class IconRegistry(ILogger<IconRegistry> logger)
{
    private static readonly FrozenDictionary<string, (MaterialIconKind Regular, MaterialIconKind Active)> Glyphs = new[]
    {
        Entry(IconIds.Unknown, MaterialIconKind.HelpRhombusOutline),
        Entry(IconIds.NewProject, MaterialIconKind.FolderPlus),
        Entry(IconIds.OpenProject, MaterialIconKind.FolderOpen),
        Entry(IconIds.Save, MaterialIconKind.ContentSave),
        Entry(IconIds.SaveAll, MaterialIconKind.ContentSaveAll),
        Entry(IconIds.ProjectSettings, MaterialIconKind.CogOutline),
        Entry(IconIds.References, MaterialIconKind.BookMultiple),
        Entry(IconIds.Exit, MaterialIconKind.ExitToApp),
        Entry(IconIds.Undo, MaterialIconKind.Undo),
        Entry(IconIds.Redo, MaterialIconKind.Redo),
        Entry(IconIds.Delete, MaterialIconKind.Delete),
        Entry(IconIds.Rename, MaterialIconKind.RenameBox),
        Entry(IconIds.SelectAll, MaterialIconKind.SelectAll),
        Entry(IconIds.AddMember, MaterialIconKind.PlusBox),
        Entry(IconIds.ClassSettings, MaterialIconKind.Cog),
        Entry(IconIds.KeyboardShortcuts, MaterialIconKind.Keyboard),
        Entry(IconIds.Home, MaterialIconKind.Home),
        Entry(IconIds.About, MaterialIconKind.InformationOutline),
        Entry(IconIds.Compile, MaterialIconKind.Hammer),
        Entry(IconIds.Run, MaterialIconKind.Play),
        Entry(IconIds.Stop, MaterialIconKind.Stop),
        Entry(IconIds.ZoomToFit, MaterialIconKind.CropFree),
        Entry(IconIds.FitToScreen, MaterialIconKind.FitToScreen),
        Entry(IconIds.FloatDocument, MaterialIconKind.OpenInNew),
        Entry(IconIds.DockDocument, MaterialIconKind.DockWindow),
        Entry(IconIds.ResetLayout, MaterialIconKind.Restore),
        Entry(IconIds.PanelProjectTree, MaterialIconKind.FileTree),
        Entry(IconIds.PanelInspector, MaterialIconKind.Tune),
        Entry(IconIds.PanelVariables, MaterialIconKind.Variable),
        Entry(IconIds.PanelErrors, MaterialIconKind.AlertCircleOutline),
        Entry(IconIds.PanelOutput, MaterialIconKind.Console),
        Entry(IconIds.PanelCSharp, MaterialIconKind.LanguageCsharp),
        Entry(IconIds.TemplateConsole, MaterialIconKind.ConsoleLine),
        Entry(IconIds.TemplateLibrary, MaterialIconKind.BookOpenVariant),
        Entry(IconIds.Project, MaterialIconKind.FolderOutline, MaterialIconKind.Folder),
        Entry(IconIds.Class, MaterialIconKind.CodeBraces),
        Entry(IconIds.Group, MaterialIconKind.FormatListBulleted),
        Entry(IconIds.Method, MaterialIconKind.FunctionVariant),
        Entry(IconIds.Constructor, MaterialIconKind.Hammer),
        Entry(IconIds.Variable, MaterialIconKind.Variable),
        Entry(IconIds.Event, MaterialIconKind.LightningBolt),
        Entry(IconIds.Add, MaterialIconKind.Plus),
        Entry(IconIds.Remove, MaterialIconKind.Minus),
        Entry(IconIds.MoveUp, MaterialIconKind.ArrowUp),
        Entry(IconIds.MoveDown, MaterialIconKind.ArrowDown),
        Entry(IconIds.Close, MaterialIconKind.Close),
        Entry(IconIds.Pin, MaterialIconKind.PinOutline, MaterialIconKind.Pin),
        Entry(IconIds.ChevronRight, MaterialIconKind.ChevronRight),
        Entry(IconIds.ChevronDown, MaterialIconKind.ChevronDown),
        Entry(IconIds.Overloads, MaterialIconKind.UnfoldMoreHorizontal),
        Entry(IconIds.CardSamples, MaterialIconKind.FlaskOutline),
        Entry(IconIds.CardLearn, MaterialIconKind.SchoolOutline),
        Entry(IconIds.CardNewProject, MaterialIconKind.PlusCircleOutline),
        Entry(IconIds.CardOpenProject, MaterialIconKind.FolderOpenOutline),
        Entry(IconIds.SeverityError, MaterialIconKind.CloseCircle),
        Entry(IconIds.SeverityWarning, MaterialIconKind.AlertCircle),
        Entry(IconIds.SeverityInfo, MaterialIconKind.InformationCircle),
        Entry(IconIds.CategoryConditionalRule, MaterialIconKind.SourceBranch),
        Entry(IconIds.CategoryConvert, MaterialIconKind.SwapHorizontal),
        Entry(IconIds.CategoryCreate, MaterialIconKind.PlusBoxOutline),
        Entry(IconIds.CategoryDelegate, MaterialIconKind.Lambda),
        Entry(IconIds.CategoryIf, MaterialIconKind.CallSplit),
        Entry(IconIds.CategoryListView, MaterialIconKind.ViewList),
        Entry(IconIds.CategoryLiteral, MaterialIconKind.CodeString),
        Entry(IconIds.CategoryLoop, MaterialIconKind.Repeat),
        Entry(IconIds.CategoryMethod, MaterialIconKind.FunctionVariant),
        Entry(IconIds.CategoryNone, MaterialIconKind.CircleOutline),
        Entry(IconIds.CategoryOperator, MaterialIconKind.PlusMinusVariant),
        Entry(IconIds.CategoryProperty, MaterialIconKind.TagOutline),
        Entry(IconIds.CategoryReturn, MaterialIconKind.KeyboardReturn),
        Entry(IconIds.CategoryTask, MaterialIconKind.ClockOutline),
        Entry(IconIds.CategoryThrow, MaterialIconKind.AlertOctagon),
        Entry(IconIds.CategoryType, MaterialIconKind.ShapeOutline),
        Entry(IconIds.NodeKindDefault, MaterialIconKind.CircleSmall),
        Entry(IconIds.NodeKindEntry, MaterialIconKind.PlayCircleOutline),
        Entry(IconIds.NodeKindReturn, MaterialIconKind.KeyboardReturn),
        Entry(IconIds.NodeKindCallMethod, MaterialIconKind.FunctionVariant),
        Entry(IconIds.NodeKindCallStatic, MaterialIconKind.Function),
        Entry(IconIds.NodeKindConstructor, MaterialIconKind.HammerWrench),
        Entry(IconIds.NodeKindMakeDelegate, MaterialIconKind.Lambda),
        Entry(IconIds.NodeKindType, MaterialIconKind.ShapeOutline),
        Entry(IconIds.NodeKindVariableGetter, MaterialIconKind.Variable),
        Entry(IconIds.NodeKindVariableSetter, MaterialIconKind.VariableBox),
        Entry(IconIds.NodeKindMakeArray, MaterialIconKind.CodeBrackets),
        Entry(IconIds.NodeKindThrow, MaterialIconKind.AlertOctagon),
        Entry(IconIds.NodeKindTernary, MaterialIconKind.SourceFork),
        Entry(IconIds.PinExec, MaterialIconKind.PlayOutline, MaterialIconKind.Play),
        Entry(IconIds.PinData, MaterialIconKind.CircleOutline, MaterialIconKind.Circle),
        Entry(IconIds.PinType, MaterialIconKind.ShapeOutline, MaterialIconKind.Shape),
        Entry(IconIds.EmptyProject, MaterialIconKind.FolderOpenOutline),
        Entry(IconIds.EmptyErrors, MaterialIconKind.CheckCircleOutline),
        Entry(IconIds.EmptyOutput, MaterialIconKind.ConsoleLine),
        Entry(IconIds.EmptyInspector, MaterialIconKind.Tune),
        Entry(IconIds.EmptySearch, MaterialIconKind.Magnify),
        Entry(IconIds.EmptyGraph, MaterialIconKind.GraphOutline),
        Entry(IconIds.DialogError, MaterialIconKind.AlertCircle),
        Entry(IconIds.DialogWarning, MaterialIconKind.AlertOutline),
        Entry(IconIds.DialogConfirm, MaterialIconKind.HelpCircleOutline),
        Entry(IconIds.DialogInfo, MaterialIconKind.InformationOutline),
        Entry(IconIds.DialogTrust, MaterialIconKind.ShieldCheckOutline),
        Entry(IconIds.DialogRecover, MaterialIconKind.BackupRestore),
    }.ToFrozenDictionary(entry => entry.Id, entry => (entry.Regular, entry.Active));

    private readonly ConcurrentDictionary<string, byte> warned = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the registry the icon presenters use; the host replaces it with one that logs.</summary>
    public static IconRegistry Shared { get; set; } = new(NullLogger<IconRegistry>.Instance);

    /// <summary>Determines whether the registry knows an id.</summary>
    /// <param name="id">The icon id.</param>
    /// <returns><see langword="true"/> when the id resolves without the fallback.</returns>
    public bool IsKnown(string? id) => id is not null && Glyphs.ContainsKey(id);

    /// <summary>Resolves an id to its glyph, or to the <see cref="IconIds.Unknown"/> glyph with one warning per unknown id.</summary>
    /// <param name="id">The icon id; a blank id draws the fallback without a warning.</param>
    /// <param name="active">When <see langword="true"/>, the filled glyph of an outline and filled pair.</param>
    /// <returns>The glyph.</returns>
    public IconGlyph Resolve(string? id, bool active = false)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Pick(Glyphs[IconIds.Unknown], active);
        }

        if (Glyphs.TryGetValue(id, out (MaterialIconKind Regular, MaterialIconKind Active) known))
        {
            return Pick(known, active);
        }

        if (warned.TryAdd(id, 0))
        {
            Log.UnknownIcon(logger, id);
        }

        return Pick(Glyphs[IconIds.Unknown], active);
    }

    private static IconGlyph Pick((MaterialIconKind Regular, MaterialIconKind Active) pair, bool active) => new(active ? pair.Active : pair.Regular);

    private static (string Id, MaterialIconKind Regular, MaterialIconKind Active) Entry(string id, MaterialIconKind regular, MaterialIconKind? active = null) =>
        (id, regular, active ?? regular);
}
