using System.Globalization;

namespace Tdms.Core;

/// <summary>
/// One TDMS property value together with the type it was stored as. Properties are the
/// part of a TDMS file that a plain CSV throws away, so the reader keeps the original
/// type rather than flattening everything to text.
/// </summary>
public readonly struct TdmsPropertyValue : IEquatable<TdmsPropertyValue>
{
    private readonly object? _value;

    /// <summary>Creates a property value.</summary>
    /// <param name="dataType">On-disk type code.</param>
    /// <param name="value">Boxed value: <see cref="string"/>, <see cref="bool"/>,
    /// <see cref="double"/>, <see cref="long"/>, <see cref="ulong"/> or <see cref="TdmsTimestamp"/>.</param>
    public TdmsPropertyValue(TdmsDataType dataType, object? value)
    {
        DataType = dataType;
        _value = value;
    }

    /// <summary>On-disk type of the property.</summary>
    public TdmsDataType DataType { get; }

    /// <summary>The boxed value exactly as read.</summary>
    public object? Value => _value;

    /// <summary>Creates a string property.</summary>
    /// <param name="value">Text.</param>
    /// <returns>The property value.</returns>
    public static TdmsPropertyValue FromString(string value) => new(TdmsDataType.String, value);

    /// <summary>Creates an <c>f64</c> property.</summary>
    /// <param name="value">Number.</param>
    /// <returns>The property value.</returns>
    public static TdmsPropertyValue FromDouble(double value) => new(TdmsDataType.F64, value);

    /// <summary>Creates an <c>i32</c> property.</summary>
    /// <param name="value">Number.</param>
    /// <returns>The property value.</returns>
    public static TdmsPropertyValue FromInt32(int value) => new(TdmsDataType.I32, (long)value);

    /// <summary>Creates a boolean property.</summary>
    /// <param name="value">Flag.</param>
    /// <returns>The property value.</returns>
    public static TdmsPropertyValue FromBoolean(bool value) => new(TdmsDataType.Boolean, value);

    /// <summary>Creates a timestamp property.</summary>
    /// <param name="value">Instant.</param>
    /// <returns>The property value.</returns>
    public static TdmsPropertyValue FromTimestamp(TdmsTimestamp value) => new(TdmsDataType.Timestamp, value);

    /// <summary>Reads the value as a number when the type allows it.</summary>
    /// <param name="value">Receives the numeric value.</param>
    /// <returns><see langword="true"/> for numeric and boolean properties.</returns>
    public bool TryGetDouble(out double value)
    {
        switch (_value)
        {
            case double d:
                value = d;
                return true;
            case long l:
                value = l;
                return true;
            case ulong u:
                value = u;
                return true;
            case bool b:
                value = b ? 1 : 0;
                return true;
            default:
                value = 0;
                return false;
        }
    }

    /// <summary>Reads the value as a timestamp when the property is one.</summary>
    /// <param name="value">Receives the instant.</param>
    /// <returns><see langword="true"/> for timestamp properties.</returns>
    public bool TryGetTimestamp(out TdmsTimestamp value)
    {
        if (_value is TdmsTimestamp t)
        {
            value = t;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Culture independent text for display, CSV headers and comparisons.</summary>
    /// <returns>The formatted value; an empty string when the property has no value.</returns>
    public string ToInvariantString() => _value switch
    {
        null => string.Empty,
        string s => s,
        bool b => b ? "true" : "false",
        double d => DataType == TdmsDataType.F32
            ? ((float)d).ToString(CultureInfo.InvariantCulture)
            : d.ToString(CultureInfo.InvariantCulture),
        long l => l.ToString(CultureInfo.InvariantCulture),
        ulong u => u.ToString(CultureInfo.InvariantCulture),
        TdmsTimestamp t => t.ToString(),
        _ => Convert.ToString(_value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    /// <inheritdoc />
    public override string ToString() => ToInvariantString();

    /// <inheritdoc />
    public bool Equals(TdmsPropertyValue other) =>
        DataType == other.DataType && Equals(_value, other._value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TdmsPropertyValue other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine((int)DataType, _value);

    /// <summary>Equality operator.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> when type and value match.</returns>
    public static bool operator ==(TdmsPropertyValue left, TdmsPropertyValue right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> when type or value differ.</returns>
    public static bool operator !=(TdmsPropertyValue left, TdmsPropertyValue right) => !left.Equals(right);
}
