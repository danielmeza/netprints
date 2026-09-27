using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NetPrints.Core;
using NetPrints.Tests.Characterization;
using NetPrints.Tests.Samples;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests.Translator
{
    /// <summary>EX-T04 and EX-T05: class and member emitters (extension-points.md §3).</summary>
    public class EmitterTests
    {
        private static ClassGraph AllNodesClass() =>
            AllNodesFixtureFactory.CreateAllNodes(Path.Combine(Path.GetTempPath(), "AllNodes.csproj")).Classes.Single();

        private static TranslationEnvironment With(IClassEmitter[] classEmitters, IMemberEmitter[] memberEmitters) =>
            new TranslationEnvironment(NodeTranslatorRegistry.BuiltIn, classEmitters, memberEmitters);

        private static string Squash(string code) => Regex.Replace(code, @"\s+", " ");

        private sealed class DelegateClassEmitter(string id, Action<ClassEmitContext> emit) : IClassEmitter
        {
            public string Id => id;

            public void EmitClass(ClassEmitContext context) => emit(context);
        }

        private sealed class DelegateMemberEmitter(string id, Action<MemberEmitContext> emit) : IMemberEmitter
        {
            public string Id => id;

            public void EmitMember(MemberEmitContext context) => emit(context);
        }

        [Fact]
        public void NoEmittersProduceTheGoldenOutput()
        {
            string golden = File.ReadAllText(Path.Combine(SampleProjectFactory.FindRepositoryRoot(),
                "tests", "NetPrints.Core.Tests", "Fixtures", "Golden", "AllNodes.Everything.cs"));

            ClassGraph cls = AllNodesClass();

            Assert.Equal(golden, new ClassTranslator(TranslationEnvironment.BuiltIn).TranslateClass(cls));
            Assert.Equal(golden, new ClassTranslator(With([], [])).TranslateClass(cls));
        }

        [Fact]
        public void ClassAndMemberEmittersAppearInOrder()
        {
            var classEmitter = new DelegateClassEmitter("test.class", context =>
            {
                context.Usings.Add("System.Diagnostics");
                context.Usings.Add("System");
                context.Attributes.Add("System.Obsolete(\"test\")");
                context.ExtraModifiers.Add("partial");
                context.ExtraModifiers.Add("sealed");
            });
            var attributeEmitter = new DelegateClassEmitter("test.second", context => context.Attributes.Add("System.Serializable"));
            var memberEmitter = new DelegateMemberEmitter("test.member", context =>
            {
                if (context.Kind == EmittedMemberKind.Property && context.Name == "Items")
                {
                    context.DeclarePartial = true;
                    context.Attributes.Add("System.Diagnostics.DebuggerHidden");
                }

                if (context.Kind == EmittedMemberKind.Method && context.Name == "Main")
                {
                    context.Attributes.Add("System.Diagnostics.DebuggerStepThrough");
                    context.ExtraModifiers.Add("unsafe");
                    context.ExtraModifiers.Add("static");
                }

                if (context.Kind == EmittedMemberKind.Constructor)
                {
                    context.Attributes.Add("System.Diagnostics.DebuggerNonUserCode");
                }
            });

            string code = new ClassTranslator(With([classEmitter, attributeEmitter], [memberEmitter])).TranslateClass(AllNodesClass());
            string squashed = Squash(code);

            string[] inOrder =
            [
                "using System;",
                "using System.Diagnostics;",
                "namespace AllNodes",
                "[System.Obsolete(\"test\")] [System.Serializable] public sealed partial class Everything<T>",
                "[System.Diagnostics.DebuggerHidden] private static partial System.Collections.Generic.List<T> Items { get; set; }",
                "[System.Diagnostics.DebuggerNonUserCode]",
                "[System.Diagnostics.DebuggerStepThrough]",
                "public static unsafe System.Tuple<System.Int32, System.String> Main<T0>(",
            ];

            int position = -1;
            foreach (string expected in inOrder)
            {
                int found = squashed.IndexOf(expected, position + 1, StringComparison.Ordinal);
                Assert.True(found > position, $"Expected '{expected}' after position {position} in:\n{code}");
                position = found;
            }

            Assert.DoesNotContain("AllNodes.Everything<T>.Items = value;", code);
        }

        [Fact]
        public void EmittersRunInRegistryOrder()
        {
            var first = new DelegateClassEmitter("first", context => context.Attributes.Add("First"));
            var second = new DelegateClassEmitter("second", context => context.Attributes.Add("Second"));

            string code = Squash(new ClassTranslator(With([first, second], [])).TranslateClass(AllNodesClass()));

            Assert.Contains("[First] [Second] public class Everything<T>", code);
        }

        [Fact]
        public void ClassEmitterCanEditBaseTypes()
        {
            var emitter = new DelegateClassEmitter("bases", context =>
            {
                context.BaseTypes.Clear();
                context.BaseTypes.Add("Acme.Base");
            });

            string code = Squash(new ClassTranslator(With([emitter], [])).TranslateClass(AllNodesClass()));

            Assert.Contains("class Everything<T> : Acme.Base {", code);
        }

        [Fact]
        public void EmitterExceptionFailsOnlyThatClass()
        {
            var emitter = new DelegateClassEmitter("test.boom", context =>
            {
                if (context.Class.Name == "Bad")
                {
                    throw new InvalidOperationException("kaboom");
                }
            });
            var translator = new ClassTranslator(With([emitter], []));
            var bad = new ClassGraph { Name = "Bad", Namespace = "Acme" };
            var good = new ClassGraph { Name = "Good", Namespace = "Acme" };

            TranslationException failure = Assert.Throws<TranslationException>(() => translator.TranslateClass(bad));
            Assert.Equal("NPT005", failure.Code);
            Assert.Equal("test.boom: kaboom", failure.Message);
            Assert.IsType<InvalidOperationException>(failure.InnerException);

            Assert.Contains("class Good", translator.TranslateClass(good));
        }

        [Fact]
        public void MemberEmitterExceptionBecomesNpt005()
        {
            var emitter = new DelegateMemberEmitter("test.member", _ => throw new InvalidOperationException("nope"));

            TranslationException failure = Assert.Throws<TranslationException>(
                () => new ClassTranslator(With([], [emitter])).TranslateClass(AllNodesClass()));

            Assert.Equal("NPT005", failure.Code);
            Assert.Equal("test.member: nope", failure.Message);
        }

        [Fact]
        public void InvalidEmitterOutputIsNpt007()
        {
            var badClassModifier = new DelegateClassEmitter("bad.class", context => context.ExtraModifiers.Add("public"));
            var badMemberModifier = new DelegateMemberEmitter("bad.member", context => context.ExtraModifiers.Add("async"));
            var partialMethod = new DelegateMemberEmitter("bad.partial", context =>
            {
                if (context.Kind == EmittedMemberKind.Method)
                {
                    context.DeclarePartial = true;
                }
            });

            Assert.Equal("NPT007", Assert.Throws<TranslationException>(
                () => new ClassTranslator(With([badClassModifier], [])).TranslateClass(AllNodesClass())).Code);
            Assert.Equal("NPT007", Assert.Throws<TranslationException>(
                () => new ClassTranslator(With([], [badMemberModifier])).TranslateClass(AllNodesClass())).Code);
            Assert.Equal("NPT007", Assert.Throws<TranslationException>(
                () => new ClassTranslator(With([], [partialMethod])).TranslateClass(AllNodesClass())).Code);
        }
    }
}
