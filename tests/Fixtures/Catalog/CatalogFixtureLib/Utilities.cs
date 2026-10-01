using System;
using System.Collections.Generic;
using System.Globalization;
using Fixture.Attributes;
using NetPrints.Annotations;

namespace Fixture.Utilities;

/// <summary>A static class covering parameter modifiers, default values, extension methods and annotations.</summary>
public static class Helpers
{
    /// <summary>A constant.</summary>
    public const int Answer = 42;

    /// <summary>Doubles a value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>Twice the value.</returns>
    [NetPrintsNode(DisplayName = "Double", Category = "Math", Keywords = ["twice", "multiply"])]
    [Expose(ExposeFlags.Callable)]
    public static int Double(int value) => value * 2;

    /// <summary>Parses text, returning success and the value through an out parameter.</summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The parsed value.</param>
    /// <returns>True when parsing succeeded.</returns>
    [Expose(ExposeFlags.Callable | ExposeFlags.Readable)]
    public static bool TryParse(string text, out int value) => int.TryParse(text, CultureInfo.InvariantCulture, out value);

    /// <summary>Swaps two values by reference.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    public static void Swap<T>(ref T left, ref T right) => (left, right) = (right, left);

    /// <summary>Reads a large value by readonly reference.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The vector length.</returns>
    public static float LengthOf(in Geometry.Vector2 value) => value.Length;

    /// <summary>Sums a variable number of values.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The sum.</returns>
    public static int Sum(params int[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        int total = 0;
        foreach (int value in values)
        {
            total += value;
        }

        return total;
    }

    /// <summary>Formats text with default values of several kinds.</summary>
    /// <param name="text">The text.</param>
    /// <param name="width">The minimum width.</param>
    /// <param name="fill">The fill character.</param>
    /// <param name="separator">The separator, null by default.</param>
    /// <param name="mode">The mode.</param>
    /// <param name="ratio">The ratio.</param>
    /// <returns>The formatted text.</returns>
    public static string Format(string text, int width = 10, char fill = ' ', string? separator = null, DayOfWeek mode = DayOfWeek.Monday, double ratio = 0.5) =>
        text.PadLeft(width, fill) + separator + mode.ToString() + ratio.ToString(CultureInfo.InvariantCulture);

    /// <summary>An extension method.</summary>
    /// <param name="text">The text to reverse.</param>
    /// <returns>The reversed text.</returns>
    public static string Reverse(this string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        char[] characters = text.ToCharArray();
        Array.Reverse(characters);
        return new string(characters);
    }

    /// <summary>A generic extension method.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The sequence.</param>
    /// <returns>The first element or the default.</returns>
    public static T? FirstOrNothing<T>(this IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (T item in source)
        {
            return item;
        }

        return default;
    }

    /// <summary>Not public: an annotation here is ignored with a warning.</summary>
    /// <returns>Zero.</returns>
    [NetPrintsNode]
    internal static int Hidden() => 0;
}

/// <summary>A type with annotations, an ignored member and a nested enum.</summary>
[NetPrintsType(DisplayName = "Counter Widget", Category = "Widgets")]
public class Counter
{
    /// <summary>Kinds of steps.</summary>
    public enum StepKind
    {
        /// <summary>Count up.</summary>
        Up,

        /// <summary>Count down.</summary>
        Down,
    }

    /// <summary>Gets or sets the count.</summary>
    [Expose(ExposeFlags.Readable | ExposeFlags.Writable)]
    public int Count { get; set; }

    /// <summary>Gets a field-like value.</summary>
    [NetPrintsIgnore]
    public int Secret;

    /// <summary>Steps the counter.</summary>
    /// <param name="kind">The step kind.</param>
    [NetPrintsNode(Keywords = ["increment"])]
    [Expose(ExposeFlags.Callable)]
    public void Step(StepKind kind = StepKind.Up) => Count += kind == StepKind.Up ? 1 : -1;

    /// <summary>Resets the counter; excluded from catalogs by annotation.</summary>
    [NetPrintsIgnore]
    public void Reset() => Count = 0;
}

/// <summary>A type whose only annotated method is not public: the <c>annotated</c> profile must not select it.</summary>
public class InternalNodeOnly
{
    /// <summary>Gets a value.</summary>
    public int Value => 1;

    /// <summary>Not public: an annotation here is ignored with a warning.</summary>
    /// <returns>Zero.</returns>
    [NetPrintsNode]
    internal int Quiet() => 0;
}

/// <summary>Members the catalog does not list: an indexer and an event.</summary>
public class Indexed
{
    /// <summary>Occurs when the value changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets an item by position.</summary>
    /// <param name="index">The position.</param>
    /// <returns>The item.</returns>
    public int this[int index] => index;

    /// <summary>Raises <see cref="Changed"/>.</summary>
    public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}
