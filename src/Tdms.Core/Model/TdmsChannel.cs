namespace Tdms.Core.Model;

/// <summary>
/// One channel of a TDMS file: its declared data type, how many samples it holds, its
/// properties and — when the file was read with data — the samples themselves.
/// </summary>
public sealed class TdmsChannel
{
    /// <summary>Property name LabVIEW uses for the waveform start instant.</summary>
    public const string WaveformStartTimeProperty = "wf_start_time";

    /// <summary>Property name LabVIEW uses for the sample interval in seconds.</summary>
    public const string WaveformIncrementProperty = "wf_increment";

    /// <summary>Property name LabVIEW uses for the offset of the first sample in seconds.</summary>
    public const string WaveformStartOffsetProperty = "wf_start_offset";

    /// <summary>Property name LabVIEW uses for the number of samples in the waveform.</summary>
    public const string WaveformSamplesProperty = "wf_samples";

    internal TdmsChannel(
        string path,
        string groupName,
        string name,
        TdmsDataType dataType,
        long sampleCount,
        IReadOnlyDictionary<string, TdmsPropertyValue> properties,
        TdmsSampleBuffer? data)
    {
        Path = path;
        GroupName = groupName;
        Name = name;
        DataType = dataType;
        SampleCount = sampleCount;
        Properties = properties;
        Data = data;
    }

    /// <summary>Full TDMS object path, for example <c>/'Group'/'Channel'</c>.</summary>
    public string Path { get; }

    /// <summary>Name of the group this channel belongs to.</summary>
    public string GroupName { get; }

    /// <summary>Channel name.</summary>
    public string Name { get; }

    /// <summary>Declared raw data type, or <see cref="TdmsDataType.Void"/> when the channel never carried data.</summary>
    public TdmsDataType DataType { get; }

    /// <summary>Total number of samples across every segment.</summary>
    public long SampleCount { get; }

    /// <summary>Channel level properties, in file order.</summary>
    public IReadOnlyDictionary<string, TdmsPropertyValue> Properties { get; }

    /// <summary>
    /// The decoded samples, or <see langword="null"/> when the file was read for metadata
    /// only or the channel was excluded by a read filter.
    /// </summary>
    public TdmsSampleBuffer? Data { get; }

    /// <summary>Whether the channel carries a LabVIEW waveform time axis.</summary>
    public bool HasWaveformTiming => WaveformIncrement is > 0;

    /// <summary>Sample interval in seconds from <c>wf_increment</c>, when present.</summary>
    public double? WaveformIncrement =>
        Properties.TryGetValue(WaveformIncrementProperty, out var v) && v.TryGetDouble(out var d) ? d : null;

    /// <summary>Offset of the first sample in seconds from <c>wf_start_offset</c>; 0 when absent.</summary>
    public double WaveformStartOffset =>
        Properties.TryGetValue(WaveformStartOffsetProperty, out var v) && v.TryGetDouble(out var d) ? d : 0;

    /// <summary>Absolute start instant from <c>wf_start_time</c>, when present.</summary>
    public TdmsTimestamp? WaveformStartTime =>
        Properties.TryGetValue(WaveformStartTimeProperty, out var v) && v.TryGetTimestamp(out var t) ? t : null;

    /// <summary>
    /// Relative time of a sample in seconds, derived from the waveform properties instead of
    /// stored per sample.
    /// </summary>
    /// <param name="index">Zero based sample index.</param>
    /// <param name="seconds">Receives <c>wf_start_offset + index * wf_increment</c>.</param>
    /// <returns><see langword="false"/> when the channel has no waveform timing.</returns>
    public bool TryGetRelativeTime(long index, out double seconds)
    {
        if (WaveformIncrement is not { } increment || increment <= 0)
        {
            seconds = 0;
            return false;
        }

        seconds = WaveformStartOffset + (index * increment);
        return true;
    }

    /// <summary>
    /// Absolute time of a sample, derived from <c>wf_start_time</c> and <c>wf_increment</c>.
    /// </summary>
    /// <param name="index">Zero based sample index.</param>
    /// <param name="time">Receives the absolute instant.</param>
    /// <returns><see langword="false"/> when start time or increment is missing.</returns>
    public bool TryGetAbsoluteTime(long index, out DateTimeOffset time)
    {
        if (WaveformStartTime is not { } start || !TryGetRelativeTime(index, out var seconds))
        {
            time = default;
            return false;
        }

        time = start.ToDateTimeOffset().AddSeconds(seconds);
        return true;
    }
}
