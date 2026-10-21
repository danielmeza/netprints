using System.Diagnostics.CodeAnalysis;
using System.Reflection;
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
    private const string XamlLoaderType = "CompiledAvaloniaXaml.!XamlLoader";
    private const string TryLoadMethod = "TryLoad";
    private const string LoadReason = "A view resource is a compiled XAML document looked up in an extension's assembly at run time.";

    private static readonly ConditionalWeakTable<Application, State> States = new();

    private static readonly Dictionary<string, Func<object>> Compiled = new(StringComparer.Ordinal)
    {
        [BuiltInContributions.BuiltInViewsUri] = () => new BuiltInViews(),
    };

    private static readonly ViewResourceTarget[] Targets = [new TemplatesTarget(), new DictionaryTarget(), new StylesTarget()];

    /// <summary>Makes the application hold the declared resources and no others: new ones are loaded and added, ones no longer declared are removed.</summary>
    /// <param name="application">The application.</param>
    /// <param name="declarations">The declarations of every group now in the registry, in registration order.</param>
    /// <param name="templateFailed">Reports a contributed template that threw while it was built.</param>
    /// <returns>The declarations that could not be used and were not reported by an earlier call; each is ignored.</returns>
    public static IReadOnlyList<ViewResourceFailure> Sync(Application application, IEnumerable<ViewResourceDeclaration> declarations, Action<ViewResourceDeclaration, Exception>? templateFailed = null)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(declarations);
        State state = States.GetOrCreateValue(application);
        ViewResourceDeclaration[] wanted = [.. declarations.DistinctBy(KeyOf, StringComparer.Ordinal)];
        HashSet<string> wantedKeys = [.. wanted.Select(KeyOf)];

        foreach (string key in state.Applied.Keys.Where(key => !wantedKeys.Contains(key)).ToArray())
        {
            state.Applied[key].Remove(application);
            state.Applied.Remove(key);
        }

        state.Failed.RemoveWhere(key => !wantedKeys.Contains(key));
        List<ViewResourceFailure> failures = [];
        foreach (ViewResourceDeclaration declaration in wanted)
        {
            string key = KeyOf(declaration);
            if (state.Applied.ContainsKey(key) || state.Failed.Contains(key))
            {
                continue;
            }

            try
            {
                state.Applied[key] = Add(application, declaration, templateFailed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                state.Failed.Add(key);
                failures.Add(new ViewResourceFailure(declaration, ex.Message));
            }
        }

        return failures;
    }

    private static AppliedResource Add(Application application, ViewResourceDeclaration declaration, Action<ViewResourceDeclaration, Exception>? templateFailed)
    {
        object document = Compiled.TryGetValue(declaration.Uri, out Func<object>? create) ? create() : Load(declaration);
        ViewResourceTarget target = Array.Find(Targets, candidate => candidate.Accepts(document))
            ?? throw new InvalidOperationException($"'{declaration.Uri}' is a {document.GetType().Name}, not a DataTemplates, ResourceDictionary or Styles document.");
        object applied = target.Add(application, document, declaration, templateFailed);
        return new AppliedResource(target, applied);
    }

    private static string KeyOf(ViewResourceDeclaration declaration) => declaration.Scope + "|" + declaration.Uri;

    [RequiresUnreferencedCode(LoadReason)]
    private static object Load(ViewResourceDeclaration declaration)
    {
        string uri = declaration.Uri;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed) || parsed.Scheme != "avares")
        {
            throw new InvalidOperationException($"'{uri}' is not an avares:// URI.");
        }

        return declaration.Source is { } source ? LoadFrom(source, parsed.ToString()) : AvaloniaXamlLoader.Load(EmptyServiceProvider.Instance, parsed);
    }

    [RequiresUnreferencedCode(LoadReason)]
    private static object LoadFrom(Assembly source, string uri)
    {
        MethodInfo? tryLoad = source.GetType(XamlLoaderType)?.GetMethod(TryLoadMethod, [typeof(IServiceProvider), typeof(string)]);
        return tryLoad?.Invoke(null, [EmptyServiceProvider.Instance, uri])
            ?? throw new InvalidOperationException($"'{uri}' is not a compiled resource of {source.GetName().Name}.");
    }

    private sealed class State
    {
        public Dictionary<string, AppliedResource> Applied { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Failed { get; } = new(StringComparer.Ordinal);
    }

    private sealed record AppliedResource(ViewResourceTarget Target, object Applied)
    {
        public void Remove(Application application) => Target.Remove(application, Applied);
    }

    private abstract class ViewResourceTarget
    {
        public abstract bool Accepts(object document);

        public abstract object Add(Application application, object document, ViewResourceDeclaration declaration, Action<ViewResourceDeclaration, Exception>? templateFailed);

        public abstract void Remove(Application application, object document);
    }

    private sealed class TemplatesTarget : ViewResourceTarget
    {
        public override bool Accepts(object document) => document is DataTemplates;

        public override object Add(Application application, object document, ViewResourceDeclaration declaration, Action<ViewResourceDeclaration, Exception>? templateFailed)
        {
            var templates = (DataTemplates)document;
            if (declaration.Owner == BuiltInContributions.OwnerId)
            {
                application.DataTemplates.AddRange(templates);
                return document;
            }

            string name = declaration.OwnerName.Length > 0 ? declaration.OwnerName : declaration.Owner;
            IDataTemplate[] wrapped =
            [
                .. templates.Select(template => ContainedDataTemplate.Wrap(template, name, templateFailed is null ? null : ex => templateFailed(declaration, ex))),
            ];
            application.DataTemplates.AddRange(wrapped);
            return wrapped;
        }

        public override void Remove(Application application, object document)
        {
            foreach (IDataTemplate template in document is DataTemplates templates ? (IEnumerable<IDataTemplate>)templates : (IDataTemplate[])document)
            {
                application.DataTemplates.Remove(template);
            }
        }
    }

    private sealed class DictionaryTarget : ViewResourceTarget
    {
        public override bool Accepts(object document) => document is ResourceDictionary;

        public override object Add(Application application, object document, ViewResourceDeclaration declaration, Action<ViewResourceDeclaration, Exception>? templateFailed)
        {
            application.Resources.MergedDictionaries.Add((ResourceDictionary)document);
            return document;
        }

        public override void Remove(Application application, object document) => application.Resources.MergedDictionaries.Remove((ResourceDictionary)document);
    }

    private sealed class StylesTarget : ViewResourceTarget
    {
        public override bool Accepts(object document) => document is IStyle;

        public override object Add(Application application, object document, ViewResourceDeclaration declaration, Action<ViewResourceDeclaration, Exception>? templateFailed)
        {
            application.Styles.Add((IStyle)document);
            return document;
        }

        public override void Remove(Application application, object document) => application.Styles.Remove((IStyle)document);
    }
}
