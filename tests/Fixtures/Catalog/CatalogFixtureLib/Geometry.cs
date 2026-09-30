using System;

namespace Fixture.Geometry;

/// <summary>A two-dimensional vector: struct, operators, implicit conversions, interface implementation.</summary>
/// <param name="X">The horizontal component.</param>
/// <param name="Y">The vertical component.</param>
public readonly record struct Vector2(float X, float Y) : IEquatable<Vector2>, IShape
{
    /// <summary>Gets the zero vector.</summary>
    public static Vector2 Zero => default;

    /// <summary>Gets the length of the vector.</summary>
    public float Length => MathF.Sqrt((X * X) + (Y * Y));

    /// <summary>Adds two vectors.</summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns>The sum.</returns>
    public static Vector2 operator +(Vector2 left, Vector2 right) => new(left.X + right.X, left.Y + right.Y);

    /// <summary>Negates a vector.</summary>
    /// <param name="value">The vector.</param>
    /// <returns>The negated vector.</returns>
    public static Vector2 operator -(Vector2 value) => new(-value.X, -value.Y);

    /// <summary>Converts a scalar to a vector with equal components.</summary>
    /// <param name="value">The scalar.</param>
    public static implicit operator Vector2(float value) => new(value, value);

    /// <summary>Converts a vector to its length.</summary>
    /// <param name="value">The vector.</param>
    public static explicit operator float(Vector2 value) => value.Length;

    /// <summary>Adds another vector to this one.</summary>
    /// <param name="other">The other vector.</param>
    /// <returns>The sum.</returns>
    public Vector2 Add(Vector2 other) => this + other;

    /// <inheritdoc />
    public float Area() => 0f;
}

/// <summary>Something with an area.</summary>
public interface IShape
{
    /// <summary>Computes the area.</summary>
    /// <returns>The area.</returns>
    float Area();
}

/// <summary>An abstract shape with an overridable member and a protected constructor.</summary>
public abstract class ShapeBase : IShape
{
    /// <summary>Initializes a new instance of the <see cref="ShapeBase"/> class.</summary>
    protected ShapeBase()
    {
    }

    /// <summary>Gets or sets the shape name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public abstract float Area();

    /// <summary>Describes the shape.</summary>
    /// <returns>The description.</returns>
    public virtual string Describe() => Name;
}

/// <summary>A circle.</summary>
public sealed class Circle : ShapeBase
{
    /// <summary>Gets or sets the radius.</summary>
    public float Radius { get; set; }

    /// <inheritdoc />
    public override float Area() => MathF.PI * Radius * Radius;
}
