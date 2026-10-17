using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Keeps the view resources extensions declared with <c>AddViewResources</c> in the application: a <c>DataTemplates</c> document adds its templates,
/// a <c>ResourceDictionary</c> (icon geometries) is merged, and a <c>Styles</c> file is added to the application styles. A resource is loaded when
/// its declaration appears and removed when its group is removed; the URI is resolved against the assembly it names.
/// </summary>
public static class ViewResourceLoader
{
    private static readonly ConditionalWeakTable<Application, State> States = new();

    private static readonly Dictionary<string, Func<object>> Compiled = new(StringComparer.Ordinal)
    {
        [BuiltInContributions.BuiltInViewsUri] = () => new BuiltInViews(),
    };

    private static readonly ViewResourceTarget[] Targets = [new TemplatesTarget(), new DictionaryTarget(), new StylesTarget()];

    /// <summary>Makes the application hold the declared resources and no others: new ones are loaded and added, ones no longer declared are removed.</summary>
    /// <param name="application">The application.</param>
    /// <param name="declarations">The declarations of every group now in the registry, in registration order.</param>
    /// <returns>The declarations that could not be used and were not reported by an earlier call; each is ignored.</returns>
    public static IReadOnlyList<ViewResourceFailure> Sync(Application application, IEnumerable<ViewResourceDeclaration> declarations)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(declarations);
        State state = States.GetOrCreateValue(application);
        ViewResourceDeclaration[] wanted = [.. declarations.DistinctBy(declaration => declaration.Uri, StringComparer.Ordinal)];
        HashSet<string> wantedUris = [.. wanted.Select(declaration => declaration.Uri)];

        foreach (string uri in state.Applied.Keys.Where(uri => !wantedUris.Contains(uri)).ToArray())
        {
            state.Applied[uri].Remove(application);
            state.Applied.Remove(uri);
        }

        state.Failed.RemoveWhere(uri => !wantedUris.Contains(uri));
        List<ViewResourceFailure> failures = [];
        foreach (ViewResourceDeclaration declaration in wanted)
        {
            if (state.Applied.ContainsKey(declaration.Uri) || state.Failed.Contains(declaration.Uri))
            {
                continue;
            }

            try
            {
                state.Applied[declaration.Uri] = Add(application, declaration);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                state.Failed.Add(declaration.Uri);
                failures.Add(new ViewResourceFailure(declaration, ex.Message));
            }
        }

        return failures;
    }

    private static AppliedResource Add(Application application, ViewResourceDeclaration declaration)
    {
        object document = Compiled.TryGetValue(declaration.Uri, out Func<object>? create) ? create() : LoadByUri(declaration.Uri);
        ViewResourceTarget target = Array.Find(Targets, candidate => candidate.Accepts(document))
            ?? throw new InvalidOperationException($"'{declaration.Uri}' is a {document.GetType().Name}, not a DataTemplates, ResourceDictionary or Styles document.");
        target.Add(application, document);
        return new AppliedResource(target, document);
    }

    private static object LoadByUri(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed) && parsed.Scheme == "avares"
            ? AvaloniaXamlLoader.Load(EmptyServiceProvider.Instance, parsed)
            : throw new InvalidOperationException($"'{uri}' is not an avares:// URI.");

    private sealed class State
    {
        public Dictionary<string, AppliedResource> Applied { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Failed { get; } = new(StringComparer.Ordinal);
    }

    private sealed record AppliedResource(ViewResourceTarget Target, object Document)
    {
        public void Remove(Application application) => Target.Remove(application, Document);
    }

    private abstract class ViewResourceTarget
    {
        public abstract bool Accepts(object document);

        public abstract void Add(Application application, object document);

        public abstract void Remove(Application application, object document);
    }

    private sealed class TemplatesTarget : ViewResourceTarget
    {
        public override bool Accepts(object document) => document is DataTemplates;

        public override void Add(Application application, object document) => application.DataTemplates.AddRange((DataTemplates)document);

        public override void Remove(Application application, object document)
        {
            foreach (IDataTemplate template in (DataTemplates)document)
            {
                application.DataTemplates.Remove(template);
            }
        }
    }

    private sealed class DictionaryTarget : ViewResourceTarget
    {
        public override bool Accepts(object document) => document is ResourceDictionary;

        public override void Add(Application application, object document) => application.Resources.MergedDictionaries.Add((ResourceDictionary)document);

        public override void Remove(Application application, object document) => application.Resources.MergedDictionaries.Remove((ResourceDictionary)document);
    }

    private sealed class StylesTarget : ViewResourceTarget
    {
        public override bool Accepts(object document) => document is IStyle;

        public override void Add(Application application, object document) => application.Styles.Add((IStyle)document);

        public override void Remove(Application application, object document) => application.Styles.Remove((IStyle)document);
    }
}
