using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using NetPrints.Core;
using Xunit;

namespace NetPrints.Tests.Localization
{
    /// <summary>
    /// FR-027, research R13, data-model section 11: a <see cref="LocalizedText"/> resolves through the UI culture, its
    /// parent and the neutral resources, then shows its default text; a text with no default shows the raw key.
    /// </summary>
    public class LocalizedTextTests
    {
        private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es");

        private static readonly CultureInfo SpanishMexico = CultureInfo.GetCultureInfo("es-MX");

        [Fact]
        public void AKeyWithNoTranslationShowsItsDefaultText()
        {
            ResourceManager resources = new FakeResources(new() { [CultureInfo.InvariantCulture] = [] });

            Assert.Equal("Save the class", new LocalizedText("Command.save.Label", "Save the class").Resolve(resources, Spanish));
        }

        [Fact]
        public void ATextWithNoDefaultAndNoResourceShowsTheRawKey()
        {
            ResourceManager resources = new FakeResources(new() { [CultureInfo.InvariantCulture] = [] });

            Assert.Equal("Node.add.Pin.in.data.Left", new LocalizedText("Node.add.Pin.in.data.Left").Resolve(resources, Spanish));
        }

        [Fact]
        public void TheCultureWinsOverItsParentAndTheNeutralResource()
        {
            ResourceManager resources = Resources(neutral: "Save", parent: "Guardar", exact: "Guardar (MX)");

            Assert.Equal("Guardar (MX)", new LocalizedText("K", "Default").Resolve(resources, SpanishMexico));
        }

        [Fact]
        public void TheParentCultureAnswersWhenTheCultureHasNoEntry()
        {
            ResourceManager resources = Resources(neutral: "Save", parent: "Guardar", exact: null);

            Assert.Equal("Guardar", new LocalizedText("K", "Default").Resolve(resources, SpanishMexico));
        }

        [Fact]
        public void TheNeutralResourceAnswersWhenNoCultureHasAnEntry()
        {
            ResourceManager resources = Resources(neutral: "Save", parent: null, exact: null);

            Assert.Equal("Save", new LocalizedText("K", "Default").Resolve(resources, SpanishMexico));
        }

        [Fact]
        public void NoResourcesAtAllShowTheDefaultText()
        {
            Assert.Equal("Default", new LocalizedText("K", "Default").Resolve((ResourceManager?)null, Spanish));
        }

        [Fact]
        public void ALiteralShowsItself()
        {
            Assert.Equal("Open project…", LocalizedText.FromLiteral("Open project…").Resolve((ResourceManager?)null, Spanish));
        }

        [Fact]
        public void TheFirstResourceManagerWithTheKeyAnswers()
        {
            ResourceManager first = new FakeResources(new() { [CultureInfo.InvariantCulture] = [] });
            ResourceManager second = Resources(neutral: "From second", parent: null, exact: null);

            Assert.Equal("From second", new LocalizedText("K", "Default").Resolve([first, second], Spanish));
        }

        private static FakeResources Resources(string neutral, string? parent, string? exact)
        {
            Dictionary<CultureInfo, Dictionary<string, string>> sets = new()
            {
                [CultureInfo.InvariantCulture] = new() { ["K"] = neutral },
            };
            if (parent is not null)
            {
                sets[Spanish] = new() { ["K"] = parent };
            }

            if (exact is not null)
            {
                sets[SpanishMexico] = new() { ["K"] = exact };
            }

            return new FakeResources(sets);
        }

        private sealed class FakeResources(Dictionary<CultureInfo, Dictionary<string, string>> sets) : ResourceManager
        {
            protected override ResourceSet? InternalGetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents)
                => new FakeSet(sets.GetValueOrDefault(culture) ?? []);
        }

        private sealed class FakeSet(Dictionary<string, string> entries) : ResourceSet
        {
            public override string? GetString(string name) => entries.GetValueOrDefault(name);

            public override string? GetString(string name, bool ignoreCase) => entries.GetValueOrDefault(name);
        }
    }
}
