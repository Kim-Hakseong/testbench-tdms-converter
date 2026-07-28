namespace Tdms.Core;

/// <summary>How much of a TDMS file the parser should decode.</summary>
public enum TdmsReadMode
{
    /// <summary>
    /// Decode raw samples. Whether they are kept in memory depends on
    /// <see cref="TdmsReadOptions.StoreValues"/>.
    /// </summary>
    Full,

    /// <summary>
    /// Read lead-ins and metadata only and count samples arithmetically. Raw sections are
    /// never decoded, and a seekable stream is skipped over instead of read.
    /// </summary>
    MetadataOnly,
}

/// <summary>Settings for <see cref="TdmsStreamParser"/> and <see cref="TdmsFileReader"/>.</summary>
public sealed class TdmsReadOptions
{
    /// <summary>Shared instance that reads metadata only.</summary>
    public static TdmsReadOptions MetadataOnly { get; } = new() { Mode = TdmsReadMode.MetadataOnly };

    /// <summary>How much to decode. Defaults to <see cref="TdmsReadMode.Full"/>.</summary>
    public TdmsReadMode Mode { get; init; } = TdmsReadMode.Full;

    /// <summary>
    /// Keep decoded samples in the returned document. Turn this off when a
    /// <see cref="Sink"/> consumes the data, so memory stays bounded by one chunk.
    /// </summary>
    public bool StoreValues { get; init; } = true;

    /// <summary>Optional streaming consumer of decoded samples.</summary>
    public ITdmsDataSink? Sink { get; init; }

    /// <summary>
    /// When set, only these object paths are decoded; every other channel is skipped at the
    /// byte level (its sample count is still reported).
    /// </summary>
    public IReadOnlySet<string>? ChannelFilter { get; init; }

    /// <summary>
    /// Keep the data that precedes an incomplete tail instead of throwing. Measurement files
    /// are routinely killed mid-write, so this defaults to <see langword="true"/>; the result
    /// is flagged with <see cref="Model.TdmsDocument.IsTruncated"/>.
    /// </summary>
    public bool TolerateTruncatedTail { get; init; } = true;
}
