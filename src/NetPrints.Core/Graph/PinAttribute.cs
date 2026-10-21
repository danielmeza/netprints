#nullable enable
using System;

namespace NetPrints.Graph
{
    /// <summary>
    /// Overrides the key and sets the role of a pin property. Without it the key is the property name without the
    /// <c>Pin</c> suffix and the role is <see cref="PinRole.Argument"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false)]
    public sealed class PinAttribute : Attribute
    {
        /// <summary>Creates the attribute with the convention key.</summary>
        public PinAttribute()
        {
        }

        /// <summary>Creates the attribute with an explicit key.</summary>
        /// <param name="key">The stored pin key.</param>
        public PinAttribute(string key)
        {
            Key = key;
        }

        /// <summary>Gets the explicit key, or null when the convention applies.</summary>
        public string? Key { get; }

        /// <summary>Gets or sets what the pin means to its node.</summary>
        public PinRole Role { get; set; } = PinRole.Argument;
    }
}
