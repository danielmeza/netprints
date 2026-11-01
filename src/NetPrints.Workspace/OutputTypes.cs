using System;
using NetPrints.Core;

namespace NetPrints.Workspace;

/// <summary>Maps MSBuild's <c>OutputType</c> to NetPrints' <see cref="BinaryType"/>, for the editor and the command line alike.</summary>
internal static class OutputTypes
{
    private const string Exe = "Exe";

    /// <summary>Maps the value of the <c>OutputType</c> property.</summary>
    /// <param name="outputType">The property value; <see langword="null"/> when the project does not define it.</param>
    /// <returns><see cref="BinaryType.Executable"/> for <c>Exe</c>; <see cref="BinaryType.SharedLibrary"/> for anything else, <c>WinExe</c> included.</returns>
    public static BinaryType FromProperty(string? outputType) =>
        string.Equals(outputType, Exe, StringComparison.OrdinalIgnoreCase) ? BinaryType.Executable : BinaryType.SharedLibrary;
}
