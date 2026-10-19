using System.Reflection;
using NetPrints.Extensibility.Testing.Xunit;

namespace MyExtension.Tests;

public sealed class ExtensionConformanceTests : ConformanceTests<Extension>
{
    protected override string? ExtensionFolder { get; } = typeof(ExtensionConformanceTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attribute => attribute.Key == "ExtensionFolder")?.Value;
}
