#nullable enable
using System;
using System.Collections.Generic;
using NetPrints.Compilation;
using NetPrints.Core;

namespace NetPrints.Translator;

/// <summary>Result of <see cref="ProjectTranslation.TranslateAll"/>.</summary>
/// <param name="Classes">Every class that translated successfully, by <see cref="ClassGraph.FullName"/>.</param>
/// <param name="Diagnostics">One <c>NPT</c> diagnostic per class that failed to translate.</param>
public sealed record ProjectTranslationResult(IReadOnlyDictionary<string, TranslatedClass> Classes, IReadOnlyList<CodeDiagnostic> Diagnostics);

/// <summary>
/// Translates every class of a project with a <see cref="TranslationEnvironment"/>, shared by
/// <c>NetPrints.Editor.Diagnostics.CodeAnalysisHost</c>'s live analysis and
/// <c>NetPrints.Desktop.ProjectCheck</c>'s headless one (release contract §5).
/// </summary>
public static class ProjectTranslation
{
    /// <summary>
    /// Translates every class of <paramref name="project"/>. A class that fails to translate
    /// contributes an <c>NPT</c> diagnostic (<see cref="DiagnosticMapper.FromTranslation"/>) instead of
    /// being skipped silently.
    /// </summary>
    /// <param name="project">Project whose classes are translated.</param>
    /// <param name="environment">Translation environment (node and member emitters) to translate with.</param>
    /// <returns>The successfully translated classes and any translation diagnostics.</returns>
    public static ProjectTranslationResult TranslateAll(Project project, TranslationEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(environment);

        var classes = new Dictionary<string, TranslatedClass>(StringComparer.Ordinal);
        var diagnostics = new List<CodeDiagnostic>();

        foreach (ClassGraph cls in project.Classes)
        {
            try
            {
                classes[cls.FullName] = new ClassTranslator(environment).Translate(cls);
            }
            catch (TranslationException ex)
            {
                diagnostics.Add(DiagnosticMapper.FromTranslation(ex, cls));
            }
        }

        return new ProjectTranslationResult(classes, diagnostics);
    }
}
