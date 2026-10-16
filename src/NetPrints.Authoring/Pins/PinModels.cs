using NetPrints.Roslyn;

namespace NetPrints.Authoring.Pins;

/// <summary>The six pin collections of a node.</summary>
internal enum PinKind
{
    InputExec,
    OutputExec,
    InputData,
    OutputData,
    InputType,
    OutputType,
}

/// <summary>How many pins a class chain declares in each collection.</summary>
/// <param name="InputExec">Input exec pins.</param>
/// <param name="OutputExec">Output exec pins.</param>
/// <param name="InputData">Input data pins.</param>
/// <param name="OutputData">Output data pins.</param>
/// <param name="InputType">Input type pins.</param>
/// <param name="OutputType">Output type pins.</param>
internal readonly record struct PinCounts(int InputExec, int OutputExec, int InputData, int OutputData, int InputType, int OutputType)
{
    public int Of(PinKind kind) =>
        kind switch
        {
            PinKind.InputExec => InputExec,
            PinKind.OutputExec => OutputExec,
            PinKind.InputData => InputData,
            PinKind.OutputData => OutputData,
            PinKind.InputType => InputType,
            _ => OutputType,
        };

    public PinCounts Add(PinKind kind) =>
        kind switch
        {
            PinKind.InputExec => this with { InputExec = InputExec + 1 },
            PinKind.OutputExec => this with { OutputExec = OutputExec + 1 },
            PinKind.InputData => this with { InputData = InputData + 1 },
            PinKind.OutputData => this with { OutputData = OutputData + 1 },
            PinKind.InputType => this with { InputType = InputType + 1 },
            _ => this with { OutputType = OutputType + 1 },
        };
}

/// <summary>One declared pin property.</summary>
/// <param name="Name">The property name.</param>
/// <param name="Accessibility">The accessibility keywords of the property.</param>
/// <param name="Kind">The collection the pin belongs to.</param>
/// <param name="Key">The pin key as a C# string literal.</param>
/// <param name="TypeName">The property type, fully qualified.</param>
/// <param name="Member">The <c>[PinType]</c> member name, or empty.</param>
/// <param name="DefaultExpression">The unconnected value expression from a BCL <c>[DefaultValue]</c>, or empty.</param>
internal sealed record PinModel(string Name, string Accessibility, PinKind Kind, string Key, string TypeName, string Member, string DefaultExpression);

/// <summary>A node class and the pins it declares, as the pipeline holds it.</summary>
/// <param name="Namespace">The containing namespace, or an empty string.</param>
/// <param name="Containers">The names of the containing types, outermost first.</param>
/// <param name="Name">The class name.</param>
/// <param name="IsSealed">Whether the class is sealed.</param>
/// <param name="HasBase">Whether a base class declares pins through the convention.</param>
/// <param name="Offsets">The pins the base classes declare, which this class continues after.</param>
/// <param name="Pins">The declared pins in declaration order.</param>
internal sealed record PinSetModel(string Namespace, EquatableArray<string> Containers, string Name, bool IsSealed, bool HasBase, PinCounts Offsets, EquatableArray<PinModel> Pins);
