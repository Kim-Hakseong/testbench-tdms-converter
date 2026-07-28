namespace Tdms.Core.Model;

/// <summary>
/// The parsed contents of a TDMS file: file level properties and the group/channel tree.
/// </summary>
public sealed class TdmsDocument
{
    internal TdmsDocument(
        IReadOnlyDictionary<string, TdmsPropertyValue> properties,
        IReadOnlyList<TdmsGroup> groups,
        int segmentCount,
        bool truncated)
    {
        Properties = properties;
        Groups = groups;
        SegmentCount = segmentCount;
        IsTruncated = truncated;
    }

    /// <summary>File level properties, in file order.</summary>
    public IReadOnlyDictionary<string, TdmsPropertyValue> Properties { get; }

    /// <summary>Groups, in file order.</summary>
    public IReadOnlyList<TdmsGroup> Groups { get; }

    /// <summary>Number of segments that were read.</summary>
    public int SegmentCount { get; }

    /// <summary>
    /// <see langword="true"/> when the last segment was incomplete — the writer was killed
    /// mid-write. Everything before the incomplete tail is still present.
    /// </summary>
    public bool IsTruncated { get; }

    /// <summary>Path of the file this document was read from, when known.</summary>
    public string? SourcePath { get; internal set; }

    /// <summary>Size in bytes of the file this document was read from, when known.</summary>
    public long SourceSizeBytes { get; internal set; }

    /// <summary>Whether the tree came from the <c>.tdms_index</c> sidecar instead of the data file.</summary>
    public bool ReadFromIndexFile { get; internal set; }

    /// <summary>Every channel of every group, in file order.</summary>
    public IEnumerable<TdmsChannel> Channels => Groups.SelectMany(g => g.Channels);

    /// <summary>Number of channels in the file.</summary>
    public int ChannelCount => Groups.Sum(g => g.Channels.Count);

    /// <summary>Total number of samples across every channel.</summary>
    public long TotalSampleCount => Groups.Sum(g => g.TotalSampleCount);

    /// <summary>Finds a group by name.</summary>
    /// <param name="name">Group name (ordinal comparison).</param>
    /// <returns>The group, or <see langword="null"/>.</returns>
    public TdmsGroup? FindGroup(string name) =>
        Groups.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.Ordinal));

    /// <summary>Finds a channel by group and channel name.</summary>
    /// <param name="group">Group name.</param>
    /// <param name="channel">Channel name.</param>
    /// <returns>The channel, or <see langword="null"/>.</returns>
    public TdmsChannel? FindChannel(string group, string channel) => FindGroup(group)?.FindChannel(channel);

    /// <summary>Finds a channel by its full object path.</summary>
    /// <param name="path">Path such as <c>/'Group'/'Channel'</c>.</param>
    /// <returns>The channel, or <see langword="null"/>.</returns>
    public TdmsChannel? FindChannelByPath(string path) =>
        Channels.FirstOrDefault(c => string.Equals(c.Path, path, StringComparison.Ordinal));
}
