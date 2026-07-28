using System.Globalization;

namespace Tdms.Core;

/// <summary>
/// A TDMS timestamp: signed seconds since 1904-01-01 00:00:00 UTC plus a positive
/// fraction of a second expressed in units of 2^-64 s.
/// </summary>
public readonly struct TdmsTimestamp : IEquatable<TdmsTimestamp>, IComparable<TdmsTimestamp>
{
    /// <summary>1904-01-01 00:00:00 UTC — the LabVIEW epoch TDMS counts from.</summary>
    public static readonly DateTimeOffset Epoch = new(1904, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private const double FractionScale = 18446744073709551616.0; // 2^64

    /// <summary>Creates a timestamp from its two on-disk fields.</summary>
    /// <param name="seconds">Whole seconds since <see cref="Epoch"/>; may be negative.</param>
    /// <param name="fractions">Positive fraction of a second in units of 2^-64 s.</param>
    public TdmsTimestamp(long seconds, ulong fractions)
    {
        Seconds = seconds;
        Fractions = fractions;
    }

    /// <summary>Whole seconds since <see cref="Epoch"/>.</summary>
    public long Seconds { get; }

    /// <summary>Positive fraction of a second in units of 2^-64 s.</summary>
    public ulong Fractions { get; }

    /// <summary>The fractional part as a value in [0, 1).</summary>
    public double FractionalSeconds => Fractions / FractionScale;

    /// <summary>Converts to a UTC <see cref="DateTimeOffset"/> (100 ns resolution).</summary>
    /// <returns>The instant this timestamp denotes.</returns>
    public DateTimeOffset ToDateTimeOffset()
    {
        var ticks = (long)Math.Round(FractionalSeconds * TimeSpan.TicksPerSecond);
        return Epoch.AddSeconds(Seconds).AddTicks(ticks);
    }

    /// <summary>Builds a TDMS timestamp from an instant.</summary>
    /// <param name="value">Any instant; converted to UTC first.</param>
    /// <returns>The equivalent TDMS timestamp.</returns>
    public static TdmsTimestamp FromDateTimeOffset(DateTimeOffset value)
    {
        var delta = value.UtcDateTime - Epoch.UtcDateTime;
        var seconds = (long)Math.Floor(delta.TotalSeconds);
        var remainderTicks = delta.Ticks - (seconds * TimeSpan.TicksPerSecond);
        var fraction = remainderTicks / (double)TimeSpan.TicksPerSecond;
        var fractions = (ulong)Math.Min(FractionScale - 2048.0, Math.Round(fraction * FractionScale));
        return new TdmsTimestamp(seconds, fractions);
    }

    /// <summary>Seconds since the Unix epoch, as a double.</summary>
    /// <returns>Unix time in seconds including the fractional part.</returns>
    public double ToUnixSeconds() => (Seconds - 2082844800L) + FractionalSeconds;

    /// <summary>ISO 8601 UTC representation with 100 ns resolution.</summary>
    /// <returns>For example <c>2026-07-28T10:00:00.0000000Z</c>.</returns>
    public override string ToString() =>
        ToDateTimeOffset().UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public bool Equals(TdmsTimestamp other) => Seconds == other.Seconds && Fractions == other.Fractions;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TdmsTimestamp other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Seconds, Fractions);

    /// <inheritdoc />
    public int CompareTo(TdmsTimestamp other)
    {
        var bySeconds = Seconds.CompareTo(other.Seconds);
        return bySeconds != 0 ? bySeconds : Fractions.CompareTo(other.Fractions);
    }

    /// <summary>Equality operator.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> when both fields match.</returns>
    public static bool operator ==(TdmsTimestamp left, TdmsTimestamp right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> when the fields differ.</returns>
    public static bool operator !=(TdmsTimestamp left, TdmsTimestamp right) => !left.Equals(right);

    /// <summary>Less-than operator.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is earlier.</returns>
    public static bool operator <(TdmsTimestamp left, TdmsTimestamp right) => left.CompareTo(right) < 0;

    /// <summary>Greater-than operator.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is later.</returns>
    public static bool operator >(TdmsTimestamp left, TdmsTimestamp right) => left.CompareTo(right) > 0;

    /// <summary>Less-or-equal operator.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is not later.</returns>
    public static bool operator <=(TdmsTimestamp left, TdmsTimestamp right) => left.CompareTo(right) <= 0;

    /// <summary>Greater-or-equal operator.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is not earlier.</returns>
    public static bool operator >=(TdmsTimestamp left, TdmsTimestamp right) => left.CompareTo(right) >= 0;
}
