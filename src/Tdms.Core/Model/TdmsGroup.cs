namespace Tdms.Core.Model;

/// <summary>A TDMS group: a named set of channels plus its own properties.</summary>
public sealed class TdmsGroup
{
    internal TdmsGroup(
        string path,
        string name,
        IReadOnlyDictionary<string, TdmsPropertyValue> properties,
        IReadOnlyList<TdmsChannel> channels)
    {
        Path = path;
        Name = name;
        Properties = properties;
        Channels = channels;
    }

    /// <summary>Full TDMS object path, for example <c>/'Group'</c>.</summary>
    public string Path { get; }

    /// <summary>Group name.</summary>
    public string Name { get; }

    /// <summary>Group level properties, in file order.</summary>
    public IReadOnlyDictionary<string, TdmsPropertyValue> Properties { get; }

    /// <summary>Channels of this group, in file order.</summary>
    public IReadOnlyList<TdmsChannel> Channels { get; }

    /// <summary>Total number of samples across every channel of the group.</summary>
    public long TotalSampleCount => Channels.Sum(c => c.SampleCount);

    /// <summary>Finds a channel by name.</summary>
    /// <param name="name">Channel name (ordinal comparison).</param>
    /// <returns>The channel, or <see langword="null"/>.</returns>
    public TdmsChannel? FindChannel(string name) =>
        Channels.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal));
}
