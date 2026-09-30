using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace NetPrints.Catalog;

/// <summary>
/// The <see cref="ICatalogFilter"/> of a <see cref="CatalogProfile"/>: base selection, namespace and type globs,
/// attribute rules, the obsolete rule, and <c>[NetPrintsIgnore]</c>, in that order (data-model.md §2).
/// </summary>
/// <remarks>
/// With base <c>none</c> a <c>require</c> rule selects: a type matching a type rule is cataloged with all its members,
/// a member matching a member rule is cataloged with its type. With another base a <c>require</c> rule narrows the
/// base selection and an <c>exclude</c> rule drops matches.
/// </remarks>
[Experimental(ExperimentalApis.CatalogProfiles, UrlFormat = ExperimentalApis.UrlFormat)]
public sealed class CatalogProfileFilter : ICatalogFilter
{
    private readonly CatalogProfile profile;

    /// <summary>Initializes a new instance of the <see cref="CatalogProfileFilter"/> class.</summary>
    /// <param name="profile">The profile to evaluate.</param>
    public CatalogProfileFilter(CatalogProfile profile)
    {
        this.profile = Guard.NotNull(profile, nameof(profile));
    }

    /// <inheritdoc />
    public string ProfileId => profile.Id;

    /// <inheritdoc />
    public bool IncludeType(INamedTypeSymbol type)
    {
        Guard.NotNull(type, nameof(type));
        if (SymbolAttributes.Has(type, SymbolAttributes.IgnoreAttribute)
            || !PassesGlobs(type)
            || IsObsoleteExcluded(type)
            || TypeAttributesExclude(type))
        {
            return false;
        }

        bool requireMatch = RequiresMatch(profile.TypeAttributes, type);
        return profile.Base switch
        {
            CatalogProfileBase.None => requireMatch || HasIncludedMember(type),
            CatalogProfileBase.Annotated => AnnotatedSelectsType(type) && (!HasRequireRules(profile.TypeAttributes) || requireMatch),
            _ => !HasRequireRules(profile.TypeAttributes) || requireMatch,
        };
    }

    /// <inheritdoc />
    public bool IncludeMember(ISymbol member)
    {
        Guard.NotNull(member, nameof(member));
        if (SymbolAttributes.Has(member, SymbolAttributes.IgnoreAttribute)
            || IsObsoleteExcluded(member)
            || AnyRuleMatches(profile.MemberAttributes, CatalogAttributeRuleKind.Exclude, member))
        {
            return false;
        }

        bool memberRuleMatch = RequiresMatch(profile.MemberAttributes, member);
        bool requireRules = HasRequireRules(profile.MemberAttributes);
        return profile.Base switch
        {
            CatalogProfileBase.None => memberRuleMatch || (member.ContainingType is { } owner && RequiresMatch(profile.TypeAttributes, owner)),
            CatalogProfileBase.Annotated => AnnotatedSelectsMember(member) && (!requireRules || memberRuleMatch),
            _ => !requireRules || memberRuleMatch,
        };
    }

    /// <inheritdoc />
    public CatalogNodeHint? DescribeNode(ISymbol symbol) => SymbolAttributes.NodeHintOf(Guard.NotNull(symbol, nameof(symbol)));

    private static bool AnnotatedSelectsType(INamedTypeSymbol type) =>
        SymbolAttributes.Has(type, SymbolAttributes.TypeAttribute)
        || type.GetMembers().OfType<IMethodSymbol>().Any(method => SymbolAttributes.Has(method, SymbolAttributes.NodeAttribute));

    private static bool AnnotatedSelectsMember(ISymbol member) =>
        SymbolAttributes.Has(member, SymbolAttributes.NodeAttribute)
        || (member.ContainingType is { } owner && SymbolAttributes.Has(owner, SymbolAttributes.TypeAttribute));

    private static bool HasRequireRules(IReadOnlyList<CatalogAttributeRule>? rules) =>
        rules is not null && rules.Any(rule => rule.Rule == CatalogAttributeRuleKind.Require);

    private static bool RequiresMatch(IReadOnlyList<CatalogAttributeRule>? rules, ISymbol symbol) =>
        AnyRuleMatches(rules, CatalogAttributeRuleKind.Require, symbol);

    private static bool AnyRuleMatches(IReadOnlyList<CatalogAttributeRule>? rules, CatalogAttributeRuleKind kind, ISymbol symbol) =>
        rules is not null && rules.Any(rule => rule.Rule == kind && Matches(rule, symbol));

    private static bool Matches(CatalogAttributeRule rule, ISymbol symbol)
    {
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (SymbolAttributes.IsNamed(attribute, rule.Attribute) && (rule.Argument is null || ArgumentMatches(attribute, rule.Argument)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ArgumentMatches(AttributeData attribute, CatalogArgumentMatch match)
    {
        if (SymbolAttributes.Argument(attribute, match) is not { } constant || SymbolAttributes.Render(constant) is not { } rendered)
        {
            return false;
        }

        if (match.EqualsValue is { } expected)
        {
            return string.Equals(rendered.Text, expected, StringComparison.Ordinal);
        }

        if (match.ContainsValue is not { } needle)
        {
            return false;
        }

        return rendered.TokensOnly
            ? rendered.Tokens.Contains(needle, StringComparer.Ordinal)
            : rendered.Text.IndexOf(needle, StringComparison.Ordinal) >= 0;
    }

    private bool PassesGlobs(INamedTypeSymbol type)
    {
        string namespaceName = type.ContainingNamespace is { IsGlobalNamespace: false } containing ? containing.ToDisplayString() : string.Empty;
        string fullName = SymbolIds.FullName(type);
        return (profile.IncludeNamespaces is not { Count: > 0 } || Glob.AnyMatch(profile.IncludeNamespaces, namespaceName))
            && !Glob.AnyMatch(profile.ExcludeNamespaces, namespaceName)
            && (profile.IncludeTypes is not { Count: > 0 } || Glob.AnyMatch(profile.IncludeTypes, fullName))
            && !Glob.AnyMatch(profile.ExcludeTypes, fullName);
    }

    private bool TypeAttributesExclude(INamedTypeSymbol type) => AnyRuleMatches(profile.TypeAttributes, CatalogAttributeRuleKind.Exclude, type);

    private bool IsObsoleteExcluded(ISymbol symbol) =>
        SymbolAttributes.ObsoleteOf(symbol) is { } obsolete
        && profile.Obsolete switch
        {
            CatalogObsoleteMode.Exclude => true,
            CatalogObsoleteMode.ExcludeErrors => obsolete.Error,
            _ => false,
        };

    private bool HasIncludedMember(INamedTypeSymbol type) =>
        type.GetMembers().Where(Exposure.IsCatalogMember).Any(IncludeMember);
}
