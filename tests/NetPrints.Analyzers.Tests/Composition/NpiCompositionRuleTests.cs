using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Composition;

public class NpiCompositionRuleTests
{
    private const string Usings = "using System;\nusing Microsoft.Extensions.DependencyInjection;\nusing NetPrints.Composition;\nnamespace Sample;\n";

    private const string SingletonScoped = "NPI001";

    private const string InvalidRegistration = "NPI002";

    private const string AmbiguousConstructor = "NPI003";

    private const string DuplicateService = "NPI004";

    private const string SharedInstance = "NPI005";

    [Fact]
    public void ASingletonThatTakesASessionScopedServiceIsReported()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + """
            public interface ISession { }
            public interface ICache { }
            [SessionScoped<ISession>] public sealed class Session : ISession { }
            [{|NPI001:Singleton<ICache>|}] public sealed class Cache(ISession session) : ICache { }
            """,
            SingletonScoped);
    }

    [Fact]
    public void ASingletonThatReachesASessionScopedServiceThroughAFactoryIsReported()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + """
            public interface ISession { }
            public interface ICache { }
            [SessionScoped<ISession>] public sealed class Session : ISession { }
            [{|NPI001:Singleton<ICache>(Factory = nameof(Cache.Create))|}]
            public sealed class Cache : ICache
            {
                public static ICache Create(ISession session) => new Cache();
            }
            """,
            SingletonScoped);
    }

    [Fact]
    public void ASessionScopedServiceMayTakeASingletonAndASingletonMayTakeASingleton()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + """
            public interface IClock { }
            public interface ISession { }
            public interface ICache { }
            [Singleton<IClock>] public sealed class Clock : IClock { }
            [Singleton<ICache>] public sealed class Cache(IClock clock) : ICache { }
            [SessionScoped<ISession>] public sealed class Session(IClock clock, ICache cache) : ISession { }
            """,
            SingletonScoped);
    }

    [Fact]
    public void AnAbstractTypeIsReported()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + """
            public interface IClock { }
            [{|NPI002:Singleton<IClock>|}] public abstract class Clock : IClock { }
            """,
            InvalidRegistration);
    }

    [Fact]
    public void AnOpenGenericTypeIsReported()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + """
            public interface IBox { }
            [{|NPI002:Singleton<IBox>|}] public sealed class Box<T> : IBox { }
            """,
            InvalidRegistration);
    }

    [Fact]
    public void ATypeThatDoesNotImplementTheServiceIsReported()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + """
            public interface IClock { }
            [{|NPI002:Singleton<IClock>|}] public sealed class Clock { }
            """,
            InvalidRegistration);
    }

    [Fact]
    public void AFactoryThatDoesNotExistIsReported()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + """
            public interface IClock { }
            [{|NPI002:Singleton<IClock>(Factory = "Missing")|}] public sealed class Clock : IClock { }
            """,
            InvalidRegistration);
    }

    [Fact]
    public void AConcreteTypeAndAStaticFactoryClassPass()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + """
            public interface IClock { }
            public interface IMade { }
            [Singleton<Clock>] public sealed class Clock : IClock { }
            [Singleton<IMade>(Factory = nameof(Factories.Make))] public static class Factories { public static IMade Make() => throw new NotSupportedException(); }
            """,
            InvalidRegistration);
    }

    [Fact]
    public void TwoPublicConstructorsAndNoMarkerAreReported()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + """
            public interface IClock { }
            [{|NPI003:Singleton<IClock>|}]
            public sealed class Clock : IClock
            {
                public Clock() { }
                public Clock(int offset) { }
            }
            """,
            AmbiguousConstructor);
    }

    [Fact]
    public void AMarkedConstructorOrASingleConstructorPasses()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + """
            public interface IClock { }
            public interface IStore { }
            [Singleton<IClock>]
            public sealed class Clock : IClock
            {
                public Clock() { }
                [ActivatorUtilitiesConstructor] public Clock(int offset) { }
            }
            [Singleton<IStore>] public sealed class Store : IStore { public Store() { } private Store(int hidden) { } }
            """,
            AmbiguousConstructor);
    }

    [Fact]
    public void TwoRegistrationsOfOneServiceThatAConsumerTakesOnceAreReportedAtTheSecond()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + """
            public interface IClock { }
            public interface IWatch { }
            [Singleton<IClock>] public sealed class Clock : IClock { }
            [{|NPI004:Singleton<IClock>|}] public sealed class OtherClock : IClock { }
            [Singleton<IWatch>] public sealed class Watch(IClock clock) : IWatch { }
            """,
            DuplicateService);
    }

    [Fact]
    public void TwoRegistrationsOfOneServiceAConsumerTakesAsASequenceOrNotAtAllPass()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + """
            public interface IClock { }
            public interface IWatch { }
            public interface IOther { }
            [Singleton<IClock>] public sealed class Clock : IClock { }
            [Singleton<IClock>] public sealed class OtherClock : IClock { }
            [Singleton<IWatch>] public sealed class Watch(System.Collections.Generic.IEnumerable<IClock> clocks) : IWatch { }
            [Singleton<IOther>] public sealed class Other : IOther { }
            """,
            DuplicateService);
    }

    [Fact]
    public void ADisposableTypeRegisteredForTwoServicesIsReportedAtTheSecond()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + """
            public interface IA { }
            public interface IB { }
            [Singleton<IA>]
            [{|NPI005:Singleton<IB>|}]
            public sealed class Both : IA, IB, IDisposable { public void Dispose() { } }
            """,
            SharedInstance);
    }

    [Fact]
    public void ATypeRegisteredForTwoServicesWithMixedLifetimesIsReportedAtTheSecond()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + """
            public interface IA { }
            public interface IB { }
            [Singleton<IA>]
            [{|NPI005:SessionScoped<IB>|}]
            public sealed class Both : IA, IB { }
            """,
            SharedInstance);
    }

    [Fact]
    public void ANonDisposableTypeRegisteredForTwoServicesPasses()
    {
        GeneratorHarness.AssertDiagnostics(
            Usings + """
            public interface IA { }
            public interface IB { }
            [Singleton<IA>]
            [Singleton<IB>]
            public sealed class Both : IA, IB { }
            """,
            SharedInstance);
    }
}
