using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.SmartEnums;

public class NpiSmartEnumRuleTests
{
    private const string Usings = "using System.Collections.Generic;\nusing System.Linq;\nusing NetPrints.SmartEnums;\nnamespace Sample;\n";

    private const string Shape = "NPI010";

    private const string Member = "NPI011";

    [Fact]
    public void ATypeThatIsNotAbstractIsReported()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + "[{|NPI010:SmartEnum|}] public partial class Color { public static readonly Color Red = new(); }",
            Shape);
    }

    [Fact]
    public void ATypeThatIsNotPartialIsReported()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + "[{|NPI010:SmartEnum|}] public abstract class Color { }",
            Shape);
    }

    [Fact]
    public void ANestedTypeIsReported()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + "public static class Holder { [{|NPI010:SmartEnum|}] public abstract partial class Color { } }",
            Shape);
    }

    [Fact]
    public void AnAbstractPartialClassPasses()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + "[SmartEnum] public abstract partial class Color { public static readonly Color Red = new RedColor(); private sealed class RedColor : Color { } }",
            Shape,
            Member);
    }

    [Fact]
    public void AMemberThatIsAPropertyIsReported()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + """
            [SmartEnum]
            public abstract partial class Color
            {
                public static readonly Color Red = new RedColor();
                public static Color {|NPI011:Blue|} => new RedColor();
                private sealed class RedColor : Color { }
            }
            """,
            Member);
    }

    [Fact]
    public void AMemberThatIsNotReadonlyIsReported()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + """
            [SmartEnum]
            public abstract partial class Color
            {
                public static readonly Color Red = new RedColor();
                public static Color {|NPI011:Blue|} = new RedColor();
                private sealed class RedColor : Color { }
            }
            """,
            Member);
    }

    [Fact]
    public void ALookupThatReadsAllBeforeTheMembersAreDeclaredIsReported()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + """
            [SmartEnum]
            public abstract partial class Color
            {
                private static readonly Dictionary<string, Color> ByName = {|NPI011:All|}.ToDictionary(color => color.ToString());
                public static readonly Color Red = new RedColor();
                private sealed class RedColor : Color { }
            }
            """,
            Member);
    }

    [Fact]
    public void ALookupThatReadsAllThroughTheTypeNameBeforeTheMembersIsReported()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + """
            [SmartEnum]
            public abstract partial class Color
            {
                private static readonly int Count = Color.{|NPI011:All|}.Count;
                public static readonly Color Red = new RedColor();
                private sealed class RedColor : Color { }
            }
            """,
            Member);
    }

    [Fact]
    public void ALookupDeclaredAfterTheMembersPasses()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + """
            [SmartEnum]
            public abstract partial class Color
            {
                public static readonly Color Red = new RedColor();
                private static readonly Dictionary<string, Color> ByName = All.ToDictionary(color => color.ToString());
                private sealed class RedColor : Color { }
            }
            """,
            Member);
    }
}
