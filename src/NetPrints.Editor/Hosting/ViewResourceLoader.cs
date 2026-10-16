using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using NetPrints.Editor.Contributions;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Adds the view resources extensions declared with <c>AddViewResources</c> to the application: a <c>DataTemplates</c> document adds its templates,
/// a <c>ResourceDictionary</c> (icon geometries) is merged, and a <c>Styles</c> file is added to the application styles.
/// </summary>
public static class ViewResourceLoader
{
    private static readonly ConditionalWeakTable<Application, HashSet<string>> Applied = new();

    private static readonly ViewResourceTarget[] Targets = [new TemplatesTarget(), new DictionaryTarget(), new StylesTarget()];

    /// <summary>Adds the declared resources that the application has not got yet.</summary>
    /// <param name="application">The application.</param>
    /// <param name="declarations">The declarations, in registration order.</param>
    /// <exception cref="InvalidOperationException">A document is none of the supported kinds.</exception>
    public static void Apply(Application application, IEnumerable<ViewResourceDeclaration> declarations)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(declarations);
        HashSet<string> done = Applied.GetOrCreateValue(application);
        foreach (ViewResourceDeclaration declaration in declarations)
        {
            if (!done.Add(declaration.Uri))
            {
                continue;
            }

            object document = AvaloniaXamlLoader.Load(new Uri(declaration.Uri));
            ViewResourceTarget target = Array.Find(Targets, candidate => candidate.Accepts(document))
                ?? throw new InvalidOperationException($"'{declaration.Uri}' of '{declaration.Owner}' is not a DataTemplates, ResourceDictionary or Styles document.");
            target.Add(application, document);
        }
    }

    private abstract class ViewResourceTarget
    {
        public abstract bool Accepts(object document);

        public abstract void Add(Application application, object document);
    }

    private sealed class TemplatesTarget : ViewResourceTarget
    {
        public override bool Accepts(object document) => document is DataTemplates;

        public override void Add(Application application, object document) => application.DataTemplates.AddRange((DataTemplates)document);
    }

    private sealed class DictionaryTarget : ViewResourceTarget
    {
        public override bool Accepts(object document) => document is ResourceDictionary;

        public override void Add(Application application, object document) => application.Resources.MergedDictionaries.Add((ResourceDictionary)document);
    }

    private sealed class StylesTarget : ViewResourceTarget
    {
        public override bool Accepts(object document) => document is IStyle;

        public override void Add(Application application, object document) => application.Styles.Add((IStyle)document);
    }
}
