#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NetPrints.Projects;

/// <summary>
/// What the evaluation of one target framework of a project says about the C# language: its <c>LangVersion</c> and the
/// <c>MaxSupportedLangVersion</c> of the framework.
/// </summary>
/// <param name="Framework">The target framework moniker, such as <c>net10.0</c>.</param>
/// <param name="LangVersion">The evaluated <c>LangVersion</c>; <see langword="null"/> or empty when the project sets none.</param>
/// <param name="MaxSupportedLangVersion">The evaluated <c>MaxSupportedLangVersion</c>; <see langword="null"/> or empty when
/// the framework does not say, which is read as <see cref="CSharpLanguageVersion.MaxKnown"/>.</param>
public sealed record CSharpFrameworkInput(string Framework, string? LangVersion, string? MaxSupportedLangVersion);

/// <summary>
/// The outcome of <see cref="CSharpTarget.Resolve"/>.
/// </summary>
/// <param name="Target">The resolved target.</param>
/// <param name="Messages">The info messages the resolution produced (NPW006 to NPW008), each text once.</param>
public sealed record CSharpTargetResolution(CSharpTarget Target, IReadOnlyList<ProjectMessage> Messages);

/// <summary>
/// The C# language a project compiles with (ADR-0038): the resolved language version, the value the project specified and the
/// target frameworks. Generated code depends on this alone, so every writer of <c>.g.cs</c> gets the same bytes.
/// </summary>
public sealed record CSharpTarget
{
    private const string DefaultKeyword = "default";

    private CSharpTarget(CSharpLanguageVersion version, string specified, IReadOnlyList<string> targetFrameworks)
    {
        Version = version;
        Specified = specified;
        TargetFrameworks = targetFrameworks;
    }

    /// <summary>
    /// The target of a graph with no project: the newest version NetPrints knows and no target framework.
    /// </summary>
    public static CSharpTarget Unconstrained { get; } =
        new(CSharpLanguageVersion.MaxKnown, "latest", []);

    /// <summary>The language version generated code may use; the smallest across the target frameworks.</summary>
    public CSharpLanguageVersion Version { get; }

    /// <summary>The <c>LangVersion</c> the project specified (<c>latest</c>, <c>12.0</c>), or <c>default</c> when it set none.</summary>
    public string Specified { get; }

    /// <summary>The target frameworks the project builds for, in project order.</summary>
    public IReadOnlyList<string> TargetFrameworks { get; }

    /// <summary>
    /// Resolves the target of a project from the evaluation of each of its target frameworks (ADR-0038 decisions 1 to 3, 5).
    /// The keywords <c>latest</c>, <c>latestMajor</c>, <c>preview</c> and <c>default</c> resolve to the smaller of the
    /// framework's maximum and <see cref="CSharpLanguageVersion.MaxKnown"/> (NPW006); a project that sets no
    /// <c>LangVersion</c> gets the same version without a message; a version newer than the framework's maximum is kept
    /// (NPW007); an unknown version, or one newer than <see cref="CSharpLanguageVersion.MaxKnown"/>, resolves to it (NPW008).
    /// A multi-targeted project uses the smallest version.
    /// </summary>
    /// <param name="projectFilePath">The project file, set on the messages.</param>
    /// <param name="frameworks">One entry per target framework, in project order.</param>
    /// <returns>The target and its messages.</returns>
    public static CSharpTargetResolution Resolve(string projectFilePath, IReadOnlyList<CSharpFrameworkInput> frameworks)
    {
        ArgumentNullException.ThrowIfNull(frameworks);

        var messages = new List<ProjectMessage>();
        CSharpLanguageVersion? smallest = null;
        foreach (CSharpFrameworkInput framework in frameworks)
        {
            CSharpLanguageVersion version = ResolveFramework(projectFilePath, framework, messages);
            smallest = smallest is { } current && current < version ? current : version;
        }

        if (smallest is not { } resolved)
        {
            return new CSharpTargetResolution(Unconstrained, messages);
        }

        string specified = frameworks.Select(framework => framework.LangVersion?.Trim())
            .FirstOrDefault(text => !string.IsNullOrEmpty(text)) ?? DefaultKeyword;
        var target = new CSharpTarget(resolved, specified, [.. frameworks.Select(framework => framework.Framework)]);
        return new CSharpTargetResolution(target, [.. messages.DistinctBy(message => message.Message, StringComparer.Ordinal)]);
    }

    /// <summary>
    /// Whether code that needs at least <paramref name="required"/> compiles for this target.
    /// </summary>
    /// <param name="required">The oldest language version the code needs.</param>
    /// <returns><see langword="true"/> when <see cref="Version"/> is at least <paramref name="required"/>.</returns>
    public bool Check(CSharpLanguageVersion required) => Version >= required;

    private static CSharpLanguageVersion ResolveFramework(string projectFilePath, CSharpFrameworkInput framework, List<ProjectMessage> messages)
    {
        CSharpLanguageVersion maxKnown = CSharpLanguageVersion.MaxKnown;
        CSharpLanguageVersion frameworkMax = CSharpLanguageVersion.TryParse(framework.MaxSupportedLangVersion, out CSharpLanguageVersion parsedMax)
            ? parsedMax
            : maxKnown;
        CSharpLanguageVersion cap = frameworkMax < maxKnown ? frameworkMax : maxKnown;
        string specified = framework.LangVersion?.Trim() ?? string.Empty;

        if (specified.Length == 0)
        {
            return cap;
        }

        if (CSharpLanguageVersion.IsKeyword(specified))
        {
            messages.Add(Info(projectFilePath, ProjectMessage.LanguageVersionResolved,
                $"LangVersion '{specified}' of '{framework.Framework}' resolves to C# {cap}; pin <LangVersion>{cap}</LangVersion> to keep the generated code stable."));
            return cap;
        }

        if (!CSharpLanguageVersion.TryParse(specified, out CSharpLanguageVersion version) || version > maxKnown)
        {
            messages.Add(Info(projectFilePath, ProjectMessage.LanguageVersionUnknown,
                string.Create(CultureInfo.InvariantCulture, $"LangVersion '{specified}' of '{framework.Framework}' is unknown or newer than the editor's maximum; C# {maxKnown} is used.")));
            return maxKnown;
        }

        if (version > frameworkMax)
        {
            messages.Add(Info(projectFilePath, ProjectMessage.LanguageVersionNewerThanFramework,
                $"LangVersion '{specified}' is newer than the default of '{framework.Framework}' (C# {frameworkMax}); it is kept."));
        }

        return version;
    }

    private static ProjectMessage Info(string projectFilePath, string code, string text) =>
        new(ProjectMessageSeverity.Info, code, text, projectFilePath, null, null);
}
