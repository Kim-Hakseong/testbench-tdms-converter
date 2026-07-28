using System.Globalization;

namespace Tdms.Core;

/// <summary>
/// A growable block of decoded samples of one channel, stored in the narrowest CLR type
/// that keeps the on-disk value exact (i64/u64 never round-trip through <see cref="double"/>).
/// </summary>
/// <remarks>
/// The same class serves two roles: the accumulated data of a channel when a whole file is
/// read into memory, and the per-chunk block handed to an <see cref="ITdmsDataSink"/> while
/// streaming. In the streaming role the buffer is reused, so a sink must copy anything it
/// wants to keep beyond the callback.
/// </remarks>
public sealed class TdmsSampleBuffer
{
    private readonly List<double>? _doubles;
    private readonly List<long>? _longs;
    private readonly List<ulong>? _ulongs;
    private readonly List<string>? _strings;
    private readonly List<TdmsTimestamp>? _timestamps;

    /// <summary>Creates an empty buffer for the given type.</summary>
    /// <param name="dataType">Type of every sample in the buffer.</param>
    public TdmsSampleBuffer(TdmsDataType dataType)
    {
        DataType = dataType;
        switch (dataType)
        {
            case TdmsDataType.F32:
            case TdmsDataType.F64:
                _doubles = [];
                break;
            case TdmsDataType.U8:
            case TdmsDataType.U16:
            case TdmsDataType.U32:
            case TdmsDataType.U64:
                _ulongs = [];
                break;
            case TdmsDataType.String:
                _strings = [];
                break;
            case TdmsDataType.Timestamp:
                _timestamps = [];
                break;
            default:
                _longs = [];
                break;
        }
    }

    /// <summary>Type of every sample in the buffer.</summary>
    public TdmsDataType DataType { get; }

    /// <summary>Number of samples currently held.</summary>
    public int Count =>
        _doubles?.Count ?? _longs?.Count ?? _ulongs?.Count ?? _strings?.Count ?? _timestamps?.Count ?? 0;

    /// <summary>Drops every sample, keeping the allocated capacity.</summary>
    public void Clear()
    {
        _doubles?.Clear();
        _longs?.Clear();
        _ulongs?.Clear();
        _strings?.Clear();
        _timestamps?.Clear();
    }

    /// <summary>Appends every sample of another buffer of the same type.</summary>
    /// <param name="other">Source buffer.</param>
    /// <exception cref="ArgumentException">The buffers hold different types.</exception>
    public void Append(TdmsSampleBuffer other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other.DataType != DataType)
        {
            throw new ArgumentException("Sample buffers must hold the same data type.", nameof(other));
        }

        _doubles?.AddRange(other._doubles!);
        _longs?.AddRange(other._longs!);
        _ulongs?.AddRange(other._ulongs!);
        _strings?.AddRange(other._strings!);
        _timestamps?.AddRange(other._timestamps!);
    }

    /// <summary>The sample as a number, when the type is numeric or boolean.</summary>
    /// <param name="index">Sample index.</param>
    /// <param name="value">Receives the numeric value.</param>
    /// <returns><see langword="false"/> for strings and timestamps.</returns>
    public bool TryGetDouble(int index, out double value)
    {
        if (_doubles is not null)
        {
            value = _doubles[index];
            return true;
        }

        if (_longs is not null)
        {
            value = _longs[index];
            return true;
        }

        if (_ulongs is not null)
        {
            value = _ulongs[index];
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>The sample as a timestamp.</summary>
    /// <param name="index">Sample index.</param>
    /// <returns>The instant stored at <paramref name="index"/>.</returns>
    /// <exception cref="InvalidOperationException">The buffer does not hold timestamps.</exception>
    public TdmsTimestamp GetTimestamp(int index) => _timestamps is not null
        ? _timestamps[index]
        : throw new InvalidOperationException("This buffer does not hold timestamps.");

    /// <summary>The sample as a boxed CLR value.</summary>
    /// <param name="index">Sample index.</param>
    /// <returns><see cref="double"/>, <see cref="float"/>, <see cref="long"/>,
    /// <see cref="ulong"/>, <see cref="bool"/>, <see cref="string"/> or <see cref="TdmsTimestamp"/>.</returns>
    public object GetValue(int index)
    {
        if (_strings is not null)
        {
            return _strings[index];
        }

        if (_timestamps is not null)
        {
            return _timestamps[index];
        }

        if (_doubles is not null)
        {
            return DataType == TdmsDataType.F32 ? (float)_doubles[index] : (object)_doubles[index];
        }

        if (_ulongs is not null)
        {
            return _ulongs[index];
        }

        return DataType == TdmsDataType.Boolean ? _longs![index] != 0 : _longs![index];
    }

    /// <summary>
    /// Culture independent text for CSV output. Booleans are written as <c>1</c>/<c>0</c>,
    /// timestamps as ISO 8601 UTC.
    /// </summary>
    /// <param name="index">Sample index.</param>
    /// <returns>The formatted sample.</returns>
    public string GetText(int index)
    {
        if (_strings is not null)
        {
            return _strings[index];
        }

        if (_timestamps is not null)
        {
            return _timestamps[index].ToString();
        }

        if (_doubles is not null)
        {
            return DataType == TdmsDataType.F32
                ? ((float)_doubles[index]).ToString(CultureInfo.InvariantCulture)
                : _doubles[index].ToString(CultureInfo.InvariantCulture);
        }

        return _ulongs is not null
            ? _ulongs[index].ToString(CultureInfo.InvariantCulture)
            : _longs![index].ToString(CultureInfo.InvariantCulture);
    }

    internal void AddDouble(double value) => _doubles!.Add(value);

    internal void AddLong(long value) => _longs!.Add(value);

    internal void AddULong(ulong value) => _ulongs!.Add(value);

    internal void AddString(string value) => _strings!.Add(value);

    internal void AddTimestamp(TdmsTimestamp value) => _timestamps!.Add(value);
}
