namespace Fixture.Documentation;

/// <summary>Members documented the way real libraries do: several lines, references and paragraphs.</summary>
public static class Documented
{
    /// <summary>
    /// Adds <paramref name="left"/> to
    /// <paramref name="right"/>, as <see cref="System.Math.Max(int, int)"/> does.
    /// <para>A second   paragraph with <c>code</c>.</para>
    /// </summary>
    /// <param name="left">
    /// The left
    /// operand.
    /// </param>
    /// <param name="right">The right operand, see <see cref="System.Int32"/>.</param>
    /// <returns>
    /// The <see cref="System.Int32"/> sum,
    /// or <see langword="null"/> never.
    /// </returns>
    public static int Add(int left, int right) => left + right;
}
