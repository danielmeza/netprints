using System;
using System.Linq;
using System.Reflection;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Projects;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests.Translator
{
    /// <summary>
    /// FR-075, research R19, ADR-0038 decision 9: the execution translation context reaches the project's C# target through a
    /// default-implemented member, and literals are formatted for the target's language version.
    /// </summary>
    public class TranslatorTargetTests
    {
        private static CSharpTarget Cs12 => CSharpTarget.Resolve("P.csproj", [new CSharpFrameworkInput("net8.0", null, "12.0")]).Target;

        private static CSharpTarget Cs13 => CSharpTarget.Resolve("P.csproj", [new CSharpFrameworkInput("net9.0", null, "13.0")]).Target;

        [Fact]
        public void TargetIsADefaultImplementedMemberSoExistingTranslatorsCompileUnchanged()
        {
            PropertyInfo target = Assert.IsAssignableFrom<PropertyInfo>(typeof(IExecutionTranslationContext).GetProperty(nameof(IExecutionTranslationContext.Target)));

            Assert.False(target.GetMethod?.IsAbstract);
            Assert.Equal(typeof(CSharpTarget), target.PropertyType);
        }

        [Fact]
        public void AnEscapeCharacterUsesTheLongFormBeforeCSharp13AndTheShortFormFrom()
        {
            TypeSpecifier charType = TypeSpecifier.FromType<char>();

            Assert.Equal("'\\u001b'", TranslatorUtil.ObjectToLiteral('\u001b', charType, Cs12));
            Assert.Equal("'\\e'", TranslatorUtil.ObjectToLiteral('\u001b', charType, Cs13));
        }

        [Fact]
        public void AnEscapeCharacterInAStringFollowsTheSameRule()
        {
            TypeSpecifier stringType = TypeSpecifier.FromType<string>();

            Assert.Equal("\"a\\u001bb\"", TranslatorUtil.ObjectToLiteral("a\u001bb", stringType, Cs12));
            Assert.Equal("\"a\\eb\"", TranslatorUtil.ObjectToLiteral("a\u001bb", stringType, Cs13));
        }

        [Fact]
        public void AnEscapedBackslashBeforeTheDigitsIsNotAnEscapeCharacter()
        {
            TypeSpecifier stringType = TypeSpecifier.FromType<string>();

            Assert.Equal("\"\\\\u001b\"", TranslatorUtil.ObjectToLiteral("\\u001b", stringType, Cs13));
        }

        [Fact]
        public void TheLiteralOverloadWithoutATargetIsUnconstrained()
        {
            TypeSpecifier stringType = TypeSpecifier.FromType<string>();

            Assert.Equal(
                TranslatorUtil.ObjectToLiteral("a\u001bb", stringType, CSharpTarget.Unconstrained),
                TranslatorUtil.ObjectToLiteral("a\u001bb", stringType));
        }

        [Fact]
        public void NumbersAndBooleansDoNotDependOnTheVersion()
        {
            Assert.Equal("1.5D", TranslatorUtil.ObjectToLiteral(1.5, TypeSpecifier.FromType<double>(), Cs12));
            Assert.Equal("true", TranslatorUtil.ObjectToLiteral(true, TypeSpecifier.FromType<bool>(), Cs12));
        }

        [Fact]
        public void ATranslatedClassFormatsItsLiteralsForTheProjectsTarget()
        {
            Assert.Contains("\"a\\u001bb\"", TranslateGreeting(Cs12), StringComparison.Ordinal);
            Assert.Contains("\"a\\eb\"", TranslateGreeting(Cs13), StringComparison.Ordinal);
            Assert.Contains("\"a\\eb\"", TranslateGreeting(null), StringComparison.Ordinal);
        }

        private static string TranslateGreeting(CSharpTarget? target)
        {
            Project project = TestProjects.Create("P", "P");
            if (target is not null)
            {
                project.Snapshot = project.Snapshot with { CSharpTarget = target };
            }

            var cls = new ClassGraph { Name = "Greeter", Namespace = "P", Project = project };
            var method = new MethodGraph("Greet") { Class = cls, Visibility = MemberVisibility.Public };
            MethodSpecifier writeLine = new("WriteLine",
                [new MethodParameter("value", TypeSpecifier.FromType<string>(), MethodParameterPassType.Default, false, null)], [],
                MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType(typeof(Console)), []);
            var call = new CallMethodNode(method, writeLine);
            GraphUtil.ConnectExecPins(method.EntryNode.ExecPin, call.InputExecPins[0]);
            GraphUtil.ConnectExecPins(call.OutputExecPins[0], method.ReturnNodes.First().InputExecPins[0]);
            call.ArgumentPins[0].UnconnectedValue = "a\u001bb";
            cls.Methods.Add(method);

            return new ClassTranslator(TranslationEnvironment.BuiltIn).TranslateClass(cls);
        }
    }
}
