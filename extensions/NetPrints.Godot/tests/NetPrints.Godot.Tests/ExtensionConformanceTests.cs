using System.Reflection;
using NetPrints.Extensibility.Testing.Xunit;

namespace NetPrints.Godot.Tests;

public sealed class ExtensionConformanceTests : ConformanceTests<GodotExtension>
{
    protected override string? ExtensionFolder { get; } = typeof(ExtensionConformanceTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attribute => attribute.Key == "ExtensionFolder")?.Value;
}
