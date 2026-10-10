using System;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests.Core
{
    /// <summary>
    /// US8 (FR-070): renaming an <see cref="EventGraph"/> is one undoable change, a name used by another event
    /// graph of the class is refused, and the generated C# does not change.
    /// </summary>
    public class EventGraphRenameTests
    {
        private static ClassGraph ClassWithEventGraphs(out EventGraph first, out EventGraph second)
        {
            var cls = new ClassGraph { Name = "Renamed", Namespace = "EventGraphRename" };
            first = new EventGraph("First") { Class = cls };
            second = new EventGraph("Second") { Class = cls };
            cls.EventGraphs.Add(first);
            cls.EventGraphs.Add(second);
            _ = new EventEntryNode(first, "OnStart");
            _ = new EventEntryNode(second, "OnStop");
            return cls;
        }

        [Fact]
        public void RenamingChangesTheNameAndUndoRestoresIt()
        {
            ClassWithEventGraphs(out EventGraph first, out _);

            RenameResult result = first.Rename("Gameplay");

            Assert.Equal("Gameplay", first.Name);

            result.Undo();

            Assert.Equal("First", first.Name);
        }

        [Fact]
        public void ANameUsedByAnotherEventGraphIsRefused()
        {
            ClassWithEventGraphs(out EventGraph first, out _);

            ArgumentException refused = Assert.Throws<ArgumentException>(() => first.Rename("Second"));

            Assert.StartsWith("An event graph named 'Second' already exists", refused.Message, StringComparison.Ordinal);
            Assert.Equal("First", first.Name);
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        public void ABlankNameIsRefused(string blank)
        {
            ClassWithEventGraphs(out EventGraph first, out _);

            ArgumentException refused = Assert.Throws<ArgumentException>(() => first.Rename(blank));

            Assert.StartsWith("An event graph name cannot be blank", refused.Message, StringComparison.Ordinal);
            Assert.Equal("First", first.Name);
        }

        [Fact]
        public void TheNameIsTrimmedBeforeTheDuplicateCheck()
        {
            ClassWithEventGraphs(out EventGraph first, out _);

            Assert.Throws<ArgumentException>(() => first.Rename(" Second "));
            first.Rename("  Gameplay ");

            Assert.Equal("Gameplay", first.Name);
        }

        [Fact]
        public void KeepingTheCurrentNameIsNotADuplicate()
        {
            ClassWithEventGraphs(out EventGraph first, out _);

            first.Rename("First");

            Assert.Equal("First", first.Name);
        }

        [Fact]
        public void TheGeneratedCSharpDoesNotChange()
        {
            ClassGraph cls = ClassWithEventGraphs(out EventGraph first, out _);
            var translator = new ClassTranslator(TranslationEnvironment.BuiltIn);
            string before = translator.TranslateClass(cls);

            first.Rename("Gameplay");

            Assert.Equal(before, translator.TranslateClass(cls));
        }
    }
}
