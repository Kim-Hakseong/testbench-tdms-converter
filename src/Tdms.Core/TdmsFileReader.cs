using Tdms.Core.Model;

namespace Tdms.Core;

/// <summary>
/// Drives <see cref="TdmsStreamParser"/> over a stream or a file. Nothing bigger than the
/// read buffer (and, in a full read, the data the caller asked to keep) is ever held.
/// </summary>
public static class TdmsFileReader
{
    /// <summary>Extension of the metadata sidecar LabVIEW writes next to a <c>.tdms</c> file.</summary>
    public const string IndexFileExtension = ".tdms_index";

    private const int BufferSize = 128 * 1024;

    /// <summary>Path of the <c>.tdms_index</c> sidecar of a file, if it exists.</summary>
    /// <param name="tdmsPath">Path of the <c>.tdms</c> file.</param>
    /// <returns>The sidecar path, or <see langword="null"/> when there is none.</returns>
    public static string? FindIndexFile(string tdmsPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(tdmsPath);
        var candidate = System.IO.Path.ChangeExtension(tdmsPath, null) + IndexFileExtension;
        if (File.Exists(candidate))
        {
            return candidate;
        }

        candidate = tdmsPath + "_index";
        return File.Exists(candidate) ? candidate : null;
    }

    /// <summary>
    /// Reads the group/channel tree, every property and the sample counts without decoding
    /// any samples. Uses the <c>.tdms_index</c> sidecar when one is present, which turns the
    /// scan of a multi-gigabyte file into a read of a few hundred kilobytes.
    /// </summary>
    /// <param name="tdmsPath">Path of the <c>.tdms</c> file.</param>
    /// <param name="useIndexFile">Set to <see langword="false"/> to always read the data file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The document, with <see cref="TdmsDocument.ReadFromIndexFile"/> telling which source was used.</returns>
    public static TdmsDocument ReadMetadata(
        string tdmsPath,
        bool useIndexFile = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tdmsPath);

        var indexPath = useIndexFile ? FindIndexFile(tdmsPath) : null;
        var dataSize = new FileInfo(tdmsPath).Length;
        if (indexPath is not null)
        {
            try
            {
                var fromIndex = ReadFile(indexPath, TdmsReadOptions.MetadataOnly, cancellationToken);
                fromIndex.SourcePath = tdmsPath;
                fromIndex.SourceSizeBytes = dataSize;
                fromIndex.ReadFromIndexFile = true;
                return fromIndex;
            }
            catch (TdmsException)
            {
                // A damaged sidecar must never hide a readable data file.
            }
            catch (IOException)
            {
            }
        }

        var document = ReadFile(tdmsPath, TdmsReadOptions.MetadataOnly, cancellationToken);
        document.SourceSizeBytes = dataSize;
        return document;
    }

    /// <summary>Reads a whole file including every sample.</summary>
    /// <param name="tdmsPath">Path of the <c>.tdms</c> file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The document with <see cref="TdmsChannel.Data"/> populated.</returns>
    public static TdmsDocument Read(string tdmsPath, CancellationToken cancellationToken = default) =>
        ReadFile(tdmsPath, new TdmsReadOptions(), cancellationToken);

    /// <summary>Reads a file with explicit options.</summary>
    /// <param name="tdmsPath">Path of the file to read.</param>
    /// <param name="options">Read settings.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The parsed document.</returns>
    public static TdmsDocument ReadFile(
        string tdmsPath,
        TdmsReadOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tdmsPath);
        ArgumentNullException.ThrowIfNull(options);

        using var stream = new FileStream(
            tdmsPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            BufferSize,
            FileOptions.SequentialScan);

        var document = Read(stream, options, cancellationToken);
        document.SourcePath = tdmsPath;
        document.SourceSizeBytes = stream.Length;
        return document;
    }

    /// <summary>Reads a TDMS stream from its current position to the end.</summary>
    /// <param name="stream">Source stream; seekable streams let metadata reads skip raw sections.</param>
    /// <param name="options">Read settings.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The parsed document.</returns>
    public static TdmsDocument Read(
        Stream stream,
        TdmsReadOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        var parser = new TdmsStreamParser(options);
        var buffer = new byte[BufferSize];
        long? length = stream.CanSeek ? stream.Length : null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skip = parser.PendingSkipBytes;
            if (skip != 0 && stream.CanSeek && length is { } total)
            {
                var room = total - stream.Position;
                if (room <= 0)
                {
                    break;
                }

                var amount = skip < 0 ? room : Math.Min(skip, room);
                stream.Seek(amount, SeekOrigin.Current);
                parser.Skip(amount);
                continue;
            }

            var read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            parser.Push(buffer.AsSpan(0, read));
        }

        return parser.Finish();
    }

    /// <summary>
    /// Streams the samples of selected channels to a sink without keeping them. This is the
    /// path an export takes, and it is what makes files larger than RAM convertible.
    /// </summary>
    /// <param name="tdmsPath">Path of the <c>.tdms</c> file.</param>
    /// <param name="sink">Consumer of the decoded samples.</param>
    /// <param name="channelPaths">Object paths to decode; <see langword="null"/> decodes everything.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The document, without stored samples.</returns>
    public static TdmsDocument StreamData(
        string tdmsPath,
        ITdmsDataSink sink,
        IReadOnlyCollection<string>? channelPaths = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sink);

        var options = new TdmsReadOptions
        {
            Mode = TdmsReadMode.Full,
            StoreValues = false,
            Sink = sink,
            ChannelFilter = channelPaths is null ? null : new HashSet<string>(channelPaths, StringComparer.Ordinal),
        };
        return ReadFile(tdmsPath, options, cancellationToken);
    }
}
