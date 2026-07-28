namespace Tdms.Core;

/// <summary>Identifies the channel a streamed block of samples belongs to.</summary>
public sealed class TdmsChannelRef
{
    internal TdmsChannelRef(string path, string groupName, string name)
    {
        Path = path;
        GroupName = groupName;
        Name = name;
    }

    /// <summary>Full TDMS object path.</summary>
    public string Path { get; }

    /// <summary>Group name, or an empty string for objects above channel level.</summary>
    public string GroupName { get; }

    /// <summary>Channel name, or an empty string for objects above channel level.</summary>
    public string Name { get; }

    /// <summary>Raw data type declared by the most recent raw index.</summary>
    public TdmsDataType DataType { get; internal set; }

    /// <inheritdoc />
    public override string ToString() => Path;
}

/// <summary>Receives decoded samples while a file is streamed, so nothing has to be buffered whole.</summary>
public interface ITdmsDataSink
{
    /// <summary>
    /// Called with a block of freshly decoded samples. The buffer is reused after the call
    /// returns — copy anything that must outlive it.
    /// </summary>
    /// <param name="channel">Channel the samples belong to.</param>
    /// <param name="samples">Newly decoded samples, in file order.</param>
    void OnSamples(TdmsChannelRef channel, TdmsSampleBuffer samples);

    /// <summary>Called when every channel of one raw chunk has been delivered.</summary>
    void OnChunkCompleted();

    /// <summary>Called once when the file has been read to the end.</summary>
    void OnFinished();
}

/// <summary>Convenience base class for <see cref="ITdmsDataSink"/> implementations.</summary>
public abstract class TdmsDataSink : ITdmsDataSink
{
    /// <inheritdoc />
    public virtual void OnSamples(TdmsChannelRef channel, TdmsSampleBuffer samples)
    {
    }

    /// <inheritdoc />
    public virtual void OnChunkCompleted()
    {
    }

    /// <inheritdoc />
    public virtual void OnFinished()
    {
    }
}
