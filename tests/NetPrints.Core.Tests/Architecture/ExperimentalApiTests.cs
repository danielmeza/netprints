using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetPrints.Core;
using NetPrints.Extensibility;
using NetPrints.Tests.Samples;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests.Architecture
{
    /// <summary>AP-T02 (extensions contract §5, ADR-0017): an external project must opt in to every experimental API it uses.</summary>
    public class ExperimentalApiTests
    {
        private const string HelpLinkAnchor = "#api-stability";

        public static TheoryData<string, string> MarkedApis() => new()
        {
            { "NPXE0001", "using NetPrints.Extensibility.Hosting; class Consumer { IHostChannelFactory? Factory; }" },
            { "NPXE0002", "using NetPrints.Extensibility.Settings; class Consumer { ISettingsStore? Store; }" },
            { "NPXE0003", "using NetPrints.Translator; class Consumer { IClassEmitter? Emitter; }" },
        };

        [Theory]
        [MemberData(nameof(MarkedApis))]
        public void UsingAMarkedApiWithoutOptInIsAnErrorThatLinksToTheGuide(string id, string source)
        {
            ImmutableArray<Diagnostic> diagnostics = Compile(source, optIn: null);

            Diagnostic[] errors = [.. diagnostics.Where(diagnostic => diagnostic.Id == id && diagnostic.Severity == DiagnosticSeverity.Error)];

            Assert.NotEmpty(errors);
            Assert.All(errors, error => Assert.Contains(HelpLinkAnchor, error.Descriptor.HelpLinkUri, StringComparison.Ordinal));
        }

        [Theory]
        [MemberData(nameof(MarkedApis))]
        public void UsingAMarkedApiWithItsIdSuppressedCompiles(string id, string source)
        {
            ImmutableArray<Diagnostic> diagnostics = Compile(source, optIn: id);

            Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        }

        private const string SiteBase = "https://danielmeza.github.io/netprints/";

        [Fact]
        public void TheHelpLinkMapsToADocsPageWithAnApiStabilityHeading()
        {
            Assert.StartsWith(SiteBase, ExperimentalApiIds.UrlFormat, StringComparison.Ordinal);
            string[] parts = ExperimentalApiIds.UrlFormat[SiteBase.Length..].Split('#');
            string page = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "docs", parts[0] + ".md");

            Assert.True(File.Exists(page), $"{ExperimentalApiIds.UrlFormat} must map to an existing docs page, but {page} does not exist");
            Assert.Equal("api-stability", parts[1]);
            Assert.Contains(File.ReadLines(page), line => line.Trim() == "## API stability");
        }

        [Fact]
        public void EveryPublicSymbolThatMentionsAnExperimentalTypeIsExperimentalWithTheSameId()
        {
            Assembly[] assemblies = [typeof(ExperimentalApiIds).Assembly, typeof(IExtensionBuilder).Assembly];
            var offenders = new List<string>();

            foreach (Type type in assemblies.SelectMany(assembly => assembly.GetExportedTypes()))
            {
                string? typeId = EffectiveId(type);
                CheckSymbol(type.FullName ?? type.Name, typeId, MentionedIds(type), offenders);

                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                foreach (MemberInfo member in type.GetMembers(flags).Where(IsVisibleMember))
                {
                    CheckSymbol($"{type.FullName}.{member.Name}", ExperimentalIdOf(member) ?? typeId, MentionedIds(member), offenders);
                }
            }

            Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
        }

        private static void CheckSymbol(string symbol, string? id, IEnumerable<string> mentioned, List<string> offenders)
        {
            foreach (string mentionedId in mentioned.Distinct(StringComparer.Ordinal).Where(mentionedId => mentionedId != id))
            {
                offenders.Add($"{symbol} mentions {mentionedId} but carries {id ?? "no [Experimental]"}");
            }
        }

        private static string? ExperimentalIdOf(MemberInfo member) => member.GetCustomAttribute<ExperimentalAttribute>(inherit: false)?.DiagnosticId;

        private static string? EffectiveId(Type type) => ExperimentalIdOf(type) ?? (type.DeclaringType is { } outer ? EffectiveId(outer) : null);

        private static bool IsVisibleMember(MemberInfo member) => member switch
        {
            MethodBase method => IsExposed(method) && !IsAccessor(method),
            PropertyInfo property => (property.GetMethod is { } getter && IsExposed(getter)) || (property.SetMethod is { } setter && IsExposed(setter)),
            FieldInfo field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly,
            EventInfo evt => evt.AddMethod is { } add && IsExposed(add),
            _ => false,
        };

        private static bool IsExposed(MethodBase method) => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

        private static bool IsAccessor(MethodBase method) =>
            method.IsSpecialName && (method.Name.StartsWith("get_", StringComparison.Ordinal) || method.Name.StartsWith("set_", StringComparison.Ordinal)
                || method.Name.StartsWith("add_", StringComparison.Ordinal) || method.Name.StartsWith("remove_", StringComparison.Ordinal));

        private static IEnumerable<string> MentionedIds(MemberInfo member) => member switch
        {
            Type type => new[] { type.BaseType }.Concat(type.GetInterfaces()).OfType<Type>().SelectMany(ExperimentalIdsIn),
            MethodBase method => method.GetParameters().Select(parameter => parameter.ParameterType)
                .Concat(method is MethodInfo info ? [info.ReturnType] : [])
                .SelectMany(ExperimentalIdsIn),
            PropertyInfo property => ExperimentalIdsIn(property.PropertyType).Concat(property.GetIndexParameters().SelectMany(parameter => ExperimentalIdsIn(parameter.ParameterType))),
            FieldInfo field => ExperimentalIdsIn(field.FieldType),
            EventInfo evt => evt.EventHandlerType is { } handler ? ExperimentalIdsIn(handler) : [],
            _ => [],
        };

        private static IEnumerable<string> ExperimentalIdsIn(Type type)
        {
            if (type.IsGenericParameter)
            {
                yield break;
            }

            if (type.HasElementType && type.GetElementType() is { } element)
            {
                foreach (string id in ExperimentalIdsIn(element))
                {
                    yield return id;
                }

                yield break;
            }

            Type definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
            if (EffectiveId(definition) is { } own)
            {
                yield return own;
            }

            foreach (string id in type.IsGenericType ? type.GetGenericArguments().SelectMany(ExperimentalIdsIn) : [])
            {
                yield return id;
            }
        }

        private static ImmutableArray<Diagnostic> Compile(string source, string? optIn)
        {
            string trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
            List<MetadataReference> references =
            [
                .. trusted.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                    .Select(path => MetadataReference.CreateFromFile(path)),
                MetadataReference.CreateFromFile(typeof(IExtensionBuilder).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IClassEmitter).Assembly.Location),
            ];
            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable);
            if (optIn is not null)
            {
                options = options.WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic> { [optIn] = ReportDiagnostic.Suppress });
            }

            CSharpCompilation compilation = CSharpCompilation.Create(
                "External",
                [CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken)],
                references,
                options);

            return compilation.GetDiagnostics(TestContext.Current.CancellationToken);
        }
    }
}
