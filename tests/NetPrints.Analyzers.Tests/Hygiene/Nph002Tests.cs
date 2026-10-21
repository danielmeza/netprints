using System.Threading.Tasks;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Hygiene;

/// <summary>NPH002: a type name that abbreviates ViewModel as VM (replaces <c>SourceHygieneTests.NoTypeNameEndsInVM</c>).</summary>
public class Nph002Tests
{
    private const string Id = "NPH002";

    [Fact]
    public async Task APlantedNameThatEndsInVmIsReportedOnEveryTypeKind()
    {
        await HygieneHarness.AssertAsync(
            """
            class {|NPH002:FooVM|} { }
            struct {|NPH002:BarVM|} { }
            interface {|NPH002:IBazVM|} { }
            record {|NPH002:QuxVM|}(int X);
            record struct {|NPH002:QuuxVM|}(int X);
            enum {|NPH002:ModeVM|} { A }
            delegate void {|NPH002:HandlerVM|}();
            """,
            Id);
    }

    [Fact]
    public async Task ANameThatEmbedsVmBeforeAnUpperCaseLetterIsReported()
    {
        await HygieneHarness.AssertAsync("class {|NPH002:VMHost|} { }\nclass {|NPH002:NodeVMBase|} { }\n", Id);
    }

    [Fact]
    public async Task ViewModelMvvmAndLowerCaseVmAreAccepted()
    {
        await HygieneHarness.AssertAsync(
            """
            class FooViewModel { }
            class MvvmHelper { }
            class MVVM { }
            class Vmware { }
            class Canvm { }
            """,
            Id);
    }
}
