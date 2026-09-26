#nullable enable
using System;
using System.Text;
using System.Threading;

namespace NetPrints.Core;

/// <summary>
/// Generates document ids (node ids, member ids): a one-character prefix followed by 6 characters
/// of the lowercase Crockford base32 alphabet <c>0123456789abcdefghjkmnpqrstvwxyz</c> (about 30 bits
/// of entropy). See <see cref="IdGeneration"/> for how the active generator is selected.
/// </summary>
public interface IIdGenerator
{
    /// <summary>
    /// Returns a new id: <paramref name="prefix"/> followed by 6 characters of the id alphabet.
    /// </summary>
    /// <param name="prefix">Single character identifying the kind of id (<c>'n'</c> for nodes,
    /// <c>'m'</c> for members).</param>
    /// <returns>The new id.</returns>
    string NewId(char prefix);
}

/// <summary>
/// <see cref="IIdGenerator"/> backed by <see cref="Random.Shared"/>. Thread-safe (delegates to
/// <see cref="Random.Shared"/>, which is itself thread-safe). This is <see cref="IdGeneration"/>'s
/// default generator.
/// </summary>
public sealed class RandomIdGenerator : IIdGenerator
{
    private const string Alphabet = "0123456789abcdefghjkmnpqrstvwxyz";
    private const int IdLength = 6;

    /// <summary>
    /// The shared <see cref="RandomIdGenerator"/> instance.
    /// </summary>
    public static RandomIdGenerator Instance { get; } = new RandomIdGenerator();

    private RandomIdGenerator()
    {
    }

    /// <inheritdoc/>
    public string NewId(char prefix)
    {
        Span<char> chars = stackalloc char[IdLength];
        for (int i = 0; i < IdLength; i++)
        {
            chars[i] = Alphabet[Random.Shared.Next(Alphabet.Length)];
        }

        return string.Concat(prefix.ToString(), chars.ToString());
    }
}

/// <summary>
/// <see cref="IIdGenerator"/> backed by a seeded <see cref="Random"/>, so the same seed always
/// produces the same sequence of ids. Not thread-safe (matches <see cref="Random"/>'s own
/// contract). Used by tests (deterministic fixtures) and legacy import
/// (<see cref="NetPrints.Core.StableIds"/>).
/// </summary>
public sealed class SeededIdGenerator : IIdGenerator
{
    private const string Alphabet = "0123456789abcdefghjkmnpqrstvwxyz";
    private const int IdLength = 6;
    private readonly Random random;

    /// <summary>
    /// Creates a generator whose id sequence is fully determined by <paramref name="seed"/>.
    /// </summary>
    /// <param name="seed">Seed for the underlying <see cref="Random"/>.</param>
    public SeededIdGenerator(int seed)
    {
        random = new Random(seed);
    }

    /// <inheritdoc/>
    public string NewId(char prefix)
    {
        Span<char> chars = stackalloc char[IdLength];
        for (int i = 0; i < IdLength; i++)
        {
            chars[i] = Alphabet[random.Next(Alphabet.Length)];
        }

        return string.Concat(prefix.ToString(), chars.ToString());
    }
}

/// <summary>
/// Ambient <see cref="IIdGenerator"/> used by node and member constructors, which have no access to
/// services. The active generator flows with the current asynchronous call context (like
/// <see cref="AsyncLocal{T}"/> in general): it is visible to code awaited from within a
/// <see cref="Use"/> scope, but changes made by one parallel branch (e.g. a forked
/// <see cref="System.Threading.Tasks.Task"/>) are not visible to sibling branches or to the caller
/// after the branch completes.
/// </summary>
public static class IdGeneration
{
    private static readonly AsyncLocal<IIdGenerator?> Override = new AsyncLocal<IIdGenerator?>();

    /// <summary>
    /// The generator new ids are currently drawn from: the innermost active <see cref="Use"/> scope
    /// for the current call context, or <see cref="RandomIdGenerator.Instance"/> if there is none.
    /// </summary>
    public static IIdGenerator Current => Override.Value ?? RandomIdGenerator.Instance;

    /// <summary>
    /// Makes <paramref name="generator"/> the active generator for the current call context until
    /// the returned scope is disposed, which restores whatever generator was active before.
    /// </summary>
    /// <param name="generator">Generator to activate.</param>
    /// <returns>A scope; dispose it to restore the previous generator.</returns>
    public static IDisposable Use(IIdGenerator generator)
    {
        var previous = Override.Value;
        Override.Value = generator;
        return new RestoreScope(previous);
    }

    private sealed class RestoreScope : IDisposable
    {
        private readonly IIdGenerator? previous;
        private bool disposed;

        public RestoreScope(IIdGenerator? previous)
        {
            this.previous = previous;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Override.Value = previous;
        }
    }
}

/// <summary>
/// Deterministic seeds and validation shared by id generation and legacy import.
/// </summary>
public static class StableIds
{
    /// <summary>
    /// Returns the FNV-1a 32-bit hash of <paramref name="text"/>'s UTF-8 bytes (offset basis
    /// 2166136261, prime 16777619), cast to <see cref="int"/>. Used to seed a
    /// <see cref="SeededIdGenerator"/> deterministically from a class's full name, so converting the
    /// same legacy class twice assigns the same member ids.
    /// </summary>
    /// <param name="text">Text to hash.</param>
    /// <returns>The hash, as a signed 32-bit integer.</returns>
    public static int SeedFor(string text)
    {
        const uint OffsetBasis = 2166136261;
        const uint Prime = 16777619;

        uint hash = OffsetBasis;
        foreach (byte b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash *= Prime;
        }

        return unchecked((int)hash);
    }

    /// <summary>
    /// Returns whether <paramref name="id"/> is a well-formed document id: non-null, non-empty, and
    /// containing neither <c>'/'</c> nor any whitespace character.
    /// </summary>
    /// <param name="id">Candidate id, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="id"/> is a well-formed document id.</returns>
    public static bool IsValidDocumentId(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        foreach (char c in id)
        {
            if (c == '/' || char.IsWhiteSpace(c))
            {
                return false;
            }
        }

        return true;
    }
}
