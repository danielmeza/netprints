#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;

namespace NetPrints.Core;

/// <summary>
/// Generates document ids (node ids, member ids): a one-character prefix followed by 13 characters
/// of the lowercase Crockford base32 alphabet <c>0123456789abcdefghjkmnpqrstvwxyz</c>, the
/// zero-padded encoding of a 63-bit Snowflake-style value (<see cref="IdFormat"/>). See
/// <see cref="IdGeneration"/> for how the active generator is selected.
/// </summary>
public interface IIdGenerator
{
    /// <summary>
    /// Returns a new id: <paramref name="prefix"/> followed by the id alphabet encoding of a fresh
    /// value. Unique within this generator's session; never retried or searched for by callers
    /// (data-model.md §2).
    /// </summary>
    /// <param name="prefix">Single character identifying the kind of id (<c>'n'</c> for nodes,
    /// <c>'m'</c> for members).</param>
    /// <returns>The new id.</returns>
    string NewId(char prefix);
}

/// <summary>
/// Formats and parses ids (data-model.md §2): a one-character prefix, then <see cref="ValueDigits"/>
/// characters of lowercase Crockford base32, most-significant digit first, zero-padded so that
/// ordinal string order equals numeric value order. The single source of the id shape
/// (<see cref="Pattern"/>) other code — the DataContract/JSON validators, and later the generated
/// JSON Schema — reads from, instead of duplicating the regular expression.
/// </summary>
public static class IdFormat
{
    /// <summary>
    /// The id alphabet: Crockford base32, lowercase, excluding <c>i</c>, <c>l</c>, <c>o</c> and
    /// <c>u</c> to avoid visual ambiguity with <c>1</c>, <c>1</c>, <c>0</c> and <c>v</c>.
    /// </summary>
    public const string Alphabet = "0123456789abcdefghjkmnpqrstvwxyz";

    /// <summary>
    /// Number of alphabet characters an id's value is encoded as (13 * 5 = 65 bits, enough for the
    /// 63-bit value with the top 2 bits always zero).
    /// </summary>
    public const int ValueDigits = 13;

    /// <summary>
    /// The <see cref="Alphabet"/> as a regular-expression character class, shared by <see cref="Pattern"/>
    /// and <see cref="PatternFor"/> so the two never duplicate it.
    /// </summary>
    private const string ValueDigitsPattern = "[0-9a-hjkmnp-tv-z]{13}";

    /// <summary>
    /// Regular expression every id created by <see cref="SnowflakeIdGenerator"/> matches: a
    /// <c>'n'</c> (node) or <c>'m'</c> (member) prefix followed by <see cref="ValueDigits"/> lowercase
    /// alphabet characters. A document's ids are strict on read (research.md R21): a present id that
    /// does not match <see cref="PatternFor"/> for its prefix is replaced by a fresh one
    /// (<see cref="IsValid"/> is the exact check).
    /// </summary>
    public const string Pattern = "^[nm]" + ValueDigitsPattern + "$";

    /// <summary>
    /// Returns the regular expression every id of <paramref name="prefix"/> matches: <see cref="Pattern"/>
    /// narrowed to one prefix (<c>"^n[0-9a-hjkmnp-tv-z]{13}$"</c> for <c>'n'</c>). Used to build the
    /// generated JSON Schema's id patterns (document-format.md §6) and by <see cref="IsValid"/>.
    /// </summary>
    /// <param name="prefix">Id kind prefix.</param>
    /// <returns>The prefix-specific pattern.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="prefix"/> is not <c>'n'</c> or
    /// <c>'m'</c>.</exception>
    public static string PatternFor(char prefix) => prefix switch
    {
        'n' or 'm' => $"^{prefix}{ValueDigitsPattern}$",
        _ => throw new ArgumentOutOfRangeException(nameof(prefix), prefix, "Prefix must be 'n' or 'm'."),
    };

    /// <summary>
    /// Returns whether <paramref name="id"/> is exactly <paramref name="prefix"/> followed by
    /// <see cref="ValueDigits"/> lowercase <see cref="Alphabet"/> characters (an exact, case-sensitive
    /// match of <see cref="PatternFor"/> — unlike <see cref="TryParse"/>, upper case and the Crockford
    /// transcription aliases are not accepted). This is the reader's strict id check
    /// (document-format.md §1.4.1, §2.6): a present id that fails it is replaced by a fresh one.
    /// </summary>
    /// <param name="id">Candidate id, or <see langword="null"/>.</param>
    /// <param name="prefix">Id kind prefix the id must have.</param>
    /// <returns><see langword="true"/> if <paramref name="id"/> matches <see cref="PatternFor"/> for
    /// <paramref name="prefix"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="prefix"/> is not <c>'n'</c> or
    /// <c>'m'</c>.</exception>
    public static bool IsValid([NotNullWhen(true)] string? id, char prefix)
    {
        if (prefix is not ('n' or 'm'))
        {
            throw new ArgumentOutOfRangeException(nameof(prefix), prefix, "Prefix must be 'n' or 'm'.");
        }

        if (id is null || id.Length != ValueDigits + 1 || id[0] != prefix)
        {
            return false;
        }

        for (int i = 1; i < id.Length; i++)
        {
            if (Alphabet.IndexOf(id[i]) < 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns <paramref name="prefix"/> followed by <paramref name="value"/> encoded as
    /// <see cref="ValueDigits"/> zero-padded, most-significant-first <see cref="Alphabet"/> characters.
    /// </summary>
    /// <param name="prefix">Id kind prefix.</param>
    /// <param name="value">Non-negative value to encode.</param>
    /// <returns>The formatted id.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public static string Format(char prefix, long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Id value must not be negative.");
        }

        Span<char> chars = stackalloc char[ValueDigits + 1];
        chars[0] = prefix;

        long remaining = value;
        for (int i = ValueDigits; i >= 1; i--)
        {
            chars[i] = Alphabet[(int)(remaining & 0x1F)];
            remaining >>= 5;
        }

        return new string(chars);
    }

    /// <summary>
    /// Parses an id formatted by <see cref="Format"/>. Case-insensitive; also accepts Crockford's
    /// <c>i</c>/<c>l</c> → 1 and <c>o</c> → 0 transcription aliases in the value digits (the prefix
    /// itself must still be <c>n</c> or <c>m</c>, case-insensitive).
    /// </summary>
    /// <param name="text">Candidate id text.</param>
    /// <param name="prefix">The id's prefix, lowercase, if parsing succeeded.</param>
    /// <param name="value">The id's decoded value if parsing succeeded.</param>
    /// <returns><see langword="true"/> if <paramref name="text"/> is exactly one prefix character and
    /// <see cref="ValueDigits"/> valid alphabet (or alias) characters.</returns>
    public static bool TryParse(ReadOnlySpan<char> text, out char prefix, out long value)
    {
        prefix = '\0';
        value = 0;

        if (text.Length != ValueDigits + 1)
        {
            return false;
        }

        char candidatePrefix = char.ToLowerInvariant(text[0]);
        if (candidatePrefix is not ('n' or 'm'))
        {
            return false;
        }

        long decoded = 0;
        for (int i = 1; i < text.Length; i++)
        {
            int digit = DecodeDigit(text[i]);
            if (digit < 0)
            {
                return false;
            }

            decoded = (decoded << 5) | (uint)digit;
        }

        prefix = candidatePrefix;
        value = decoded;
        return true;
    }

    private static int DecodeDigit(char c) => char.ToLowerInvariant(c) switch
    {
        >= '0' and <= '9' => c - '0',
        'a' => 10,
        'b' => 11,
        'c' => 12,
        'd' => 13,
        'e' => 14,
        'f' => 15,
        'g' => 16,
        'h' => 17,
        'i' or 'l' => 1, // Crockford transcription aliases.
        'j' => 18,
        'k' => 19,
        'm' => 20,
        'n' => 21,
        'o' => 0, // Crockford transcription alias.
        'p' => 22,
        'q' => 23,
        'r' => 24,
        's' => 25,
        't' => 26,
        'v' => 27,
        'w' => 28,
        'x' => 29,
        'y' => 30,
        'z' => 31,
        _ => -1,
    };
}

/// <summary>
/// <see cref="IIdGenerator"/> producing monotonic, Snowflake-style ids from a clock and a session id
/// (data-model.md §2): a 63-bit value packed as 41 bits of milliseconds since <see cref="Epoch"/>, 16
/// bits of session id, and 6 bits of per-millisecond sequence, formatted by <see cref="IdFormat"/>.
/// Monotonic like ULID's monotonic mode: if the sequence would overflow within one millisecond, or the
/// clock goes backwards, a logical millisecond is advanced instead of reusing or going back — ids from
/// one generator instance are always strictly increasing. Thread-safe (an internal lock guards the
/// clock read and sequence update).
/// </summary>
public sealed class SnowflakeIdGenerator : IIdGenerator
{
    private const int SessionBits = 16;
    private const int SequenceBits = 6;
    private const long MaxSequence = (1L << SequenceBits) - 1;
    private const long MaxSessionId = (1L << SessionBits) - 1;

    /// <summary>
    /// Instant the 41-bit timestamp component counts milliseconds from (2026-01-01T00:00:00Z).
    /// </summary>
    public static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider timeProvider;
    private readonly ushort sessionId;
    private readonly Lock gate = new();
    private long lastTimestampMs = -1;
    private long sequence;

    /// <summary>
    /// Creates a generator with a random 16-bit session id, unique to this instance.
    /// </summary>
    /// <param name="timeProvider">Clock the timestamp component is read from.</param>
    public SnowflakeIdGenerator(TimeProvider timeProvider)
        : this(timeProvider, (ushort)Random.Shared.Next((int)MaxSessionId + 1))
    {
    }

    /// <summary>
    /// Creates a generator with an explicit session id.
    /// </summary>
    /// <param name="timeProvider">Clock the timestamp component is read from.</param>
    /// <param name="sessionId">16-bit session id every id from this instance carries.</param>
    public SnowflakeIdGenerator(TimeProvider timeProvider, ushort sessionId)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (sessionId > MaxSessionId)
        {
            throw new ArgumentOutOfRangeException(nameof(sessionId), sessionId, $"Session id must fit in {SessionBits} bits.");
        }

        this.timeProvider = timeProvider;
        this.sessionId = sessionId;
    }

    /// <inheritdoc/>
    public string NewId(char prefix)
    {
        long value;

        lock (gate)
        {
            long now = Math.Max(0, (long)(timeProvider.GetUtcNow() - Epoch).TotalMilliseconds);

            if (now > lastTimestampMs)
            {
                lastTimestampMs = now;
                sequence = 0;
            }
            else
            {
                sequence++;
                if (sequence > MaxSequence)
                {
                    lastTimestampMs++;
                    sequence = 0;
                }
            }

            value = (lastTimestampMs << (SessionBits + SequenceBits)) | ((long)sessionId << SequenceBits) | sequence;
        }

        return IdFormat.Format(prefix, value);
    }
}

/// <summary>
/// <see cref="IIdGenerator"/> backed by a shared <see cref="SnowflakeIdGenerator"/> reading the real
/// clock (<see cref="TimeProvider.System"/>) with a random session id chosen once per process. This is
/// <see cref="IdGeneration"/>'s default generator.
/// </summary>
public sealed class RandomIdGenerator : IIdGenerator
{
    /// <summary>
    /// The shared <see cref="RandomIdGenerator"/> instance.
    /// </summary>
    public static RandomIdGenerator Instance { get; } = new RandomIdGenerator();

    private readonly SnowflakeIdGenerator inner = new(TimeProvider.System);

    private RandomIdGenerator()
    {
    }

    /// <inheritdoc/>
    public string NewId(char prefix) => inner.NewId(prefix);
}

/// <summary>
/// <see cref="IIdGenerator"/> backed by a <see cref="SnowflakeIdGenerator"/> fixed to
/// <see cref="SnowflakeIdGenerator.Epoch"/> and a session id derived from an integer seed, so the same
/// seed always produces the same sequence of ids. Used by tests (deterministic fixtures) and legacy
/// import (<see cref="NetPrints.Core.StableIds"/>).
/// </summary>
public sealed class SeededIdGenerator : IIdGenerator
{
    private readonly SnowflakeIdGenerator inner;

    /// <summary>
    /// Creates a generator whose id sequence is fully determined by <paramref name="seed"/>.
    /// </summary>
    /// <param name="seed">Seed the fixed session id is derived from.</param>
    public SeededIdGenerator(int seed)
    {
        ushort session = unchecked((ushort)((seed ^ (seed >> 16)) & 0xFFFF));
        inner = new SnowflakeIdGenerator(new FixedTimeProvider(SnowflakeIdGenerator.Epoch), session);
    }

    /// <inheritdoc/>
    public string NewId(char prefix) => inner.NewId(prefix);

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            this.now = now;
        }

        public override DateTimeOffset GetUtcNow() => now;
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
    private const int MaxAllocateAttempts = 100;

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
    /// Returns a new id from <see cref="IdGeneration.Current"/>, retried until it is not already in
    /// <paramref name="existingIds"/>. Unlike normal allocation (<see cref="NodeGraph.AllocateNodeId"/>,
    /// a member constructor), which trusts the generator's own uniqueness guarantee, this is for
    /// repairing a document against a concrete, finite set of ids it already contains (a merge or a
    /// hand-edited file) — the generator has no way to know about those ahead of time.
    /// </summary>
    /// <param name="prefix">Id kind prefix.</param>
    /// <param name="existingIds">Ids the result must not collide with.</param>
    /// <returns>An id not in <paramref name="existingIds"/>.</returns>
    /// <exception cref="InvalidOperationException">No unused id was found in
    /// <see cref="MaxAllocateAttempts"/> tries.</exception>
    public static string AllocateUnique(char prefix, ICollection<string> existingIds)
    {
        for (int attempt = 0; attempt < MaxAllocateAttempts; attempt++)
        {
            string candidate = IdGeneration.Current.NewId(prefix);
            if (!existingIds.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Could not allocate a unique id in {MaxAllocateAttempts} attempts.");
    }
}
