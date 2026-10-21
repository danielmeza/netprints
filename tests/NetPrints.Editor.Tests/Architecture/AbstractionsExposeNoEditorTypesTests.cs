using System.Reflection;
using NetPrints.Extensibility.Editor;

namespace NetPrints.Editor.Tests.Architecture;

/// <summary>ADR-0020 Amendment 1, FR-022: no public member of the extension API assembly exposes a type of <c>NetPrints.Editor</c>, so no extension sees a view model.</summary>
public class AbstractionsExposeNoEditorTypesTests
{
    private const BindingFlags Visible = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Assembly Editor = typeof(global::NetPrints.Editor.Hosting.TaskExtensions).Assembly;

    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;
        if (type.HasElementType && type.GetElementType() is { } element)
        {
            foreach (Type inner in Flatten(element))
            {
                yield return inner;
            }
        }

        if (type.IsGenericType)
        {
            foreach (Type argument in type.GetGenericArguments())
            {
                foreach (Type inner in Flatten(argument))
                {
                    yield return inner;
                }
            }
        }
    }

    private static bool IsVisible(MemberInfo member) => member switch
    {
        MethodBase method => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly,
        FieldInfo field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly,
        _ => true,
    };

    private static IEnumerable<Type> TypesUsedBy(Type type)
    {
        IEnumerable<Type?> used = new[] { type.BaseType }.Concat(type.GetInterfaces())
            .Concat(type.GetFields(Visible).Where(IsVisible).Select(field => field.FieldType))
            .Concat(type.GetProperties(Visible).Select(property => property.PropertyType))
            .Concat(type.GetEvents(Visible).Select(item => item.EventHandlerType))
            .Concat(type.GetMethods(Visible).Where(IsVisible).SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType)))
            .Concat(type.GetConstructors(Visible).Where(IsVisible).SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType)));
        return used.OfType<Type>().SelectMany(Flatten);
    }

    [Fact]
    public void NoPublicMemberOfTheExtensionApiUsesATypeOfTheEditorAssembly()
    {
        Type[] types = [.. typeof(CommandSelection).Assembly.GetExportedTypes()];
        Assert.NotEmpty(types);

        string[] offenders = [.. types.SelectMany(type => TypesUsedBy(type).Where(used => used.Assembly == Editor).Select(used => $"{type.FullName} uses {used.FullName}")).Distinct()];

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheSelectionHandsExtensionsModelNodesNotOpaqueObjects()
    {
        Assert.Equal(typeof(IReadOnlyList<NetPrints.Graph.Node>), typeof(CommandSelection).GetProperty(nameof(CommandSelection.Nodes))?.PropertyType);
    }
}
