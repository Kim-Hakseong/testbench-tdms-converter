using System.Globalization;
using System.Text;
using Tdms.Core.Model;

namespace Tdms.Core.Export;

/// <summary>
/// Shared machinery of the CSV writers: one column per channel, rows padded with empty
/// fields where channels are ragged, and the file streamed chunk by chunk so a file larger
/// than RAM converts in bounded memory.
/// </summary>
public abstract class CsvExporterBase : ITdmsExporter
{
    /// <summary>Name of the derived time column.</summary>
    public const string TimeColumnHeader = "Time (s)";

    private const int ProgressEveryRows = 4096;

    /// <inheritdoc />
    public abstract string Id { get; }

    /// <inheritdoc />
    public abstract string DisplayNameKey { get; }

    /// <inheritdoc />
    public string FileExtension => ".csv";

    /// <summary>Whether the TDMS properties are written as leading <c>#</c> comment lines.</summary>
    protected abstract bool WritePropertyHeader { get; }

    /// <inheritdoc />
    public void Export(TdmsExportRequest request, Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(output);

        var metadata = request.Metadata ?? TdmsFileReader.ReadMetadata(request.SourcePath, true, cancellationToken);
        var channels = new List<TdmsChannel>(request.ChannelPaths.Count);
        foreach (var path in request.ChannelPaths)
        {
            channels.Add(metadata.FindChannelByPath(path)
                ?? throw new TdmsException($"Channel {path} is not present in {request.SourcePath}."));
        }

        if (channels.Count == 0)
        {
            throw new TdmsException("Select at least one channel to export.");
        }

        using var writer = new StreamWriter(output, new UTF8Encoding(false), 64 * 1024, leaveOpen: true)
        {
            NewLine = "\n",
        };

        if (WritePropertyHeader)
        {
            WriteProperties(writer, metadata, channels);
        }

        var timeChannel = request.IncludeWaveformTimeColumn
            ? channels.FirstOrDefault(c => c.HasWaveformTiming)
            : null;

        var header = new StringBuilder();
        if (timeChannel is not null)
        {
            header.Append(Escape(TimeColumnHeader, request.Delimiter)).Append(request.Delimiter);
        }

        for (var i = 0; i < channels.Count; i++)
        {
            if (i > 0)
            {
                header.Append(request.Delimiter);
            }

            header.Append(Escape($"{channels[i].GroupName}/{channels[i].Name}", request.Delimiter));
        }

        writer.WriteLine(header.ToString());

        using var sink = new CsvRowSink(writer, channels, timeChannel, request);
        TdmsFileReader.StreamData(
            request.SourcePath,
            sink,
            channels.Select(c => c.Path).ToList(),
            cancellationToken);
        sink.Complete();
        writer.Flush();
    }

    /// <summary>Quotes a field when it contains the delimiter, a quote or a line break.</summary>
    /// <param name="value">Raw field text.</param>
    /// <param name="delimiter">Field separator in use.</param>
    /// <returns>The field ready to be written.</returns>
    protected static string Escape(string value, char delimiter)
    {
        if (value.Length == 0)
        {
            return value;
        }

        var needsQuotes = value.Contains(delimiter) ||
            value.Contains('"') ||
            value.Contains('\n') ||
            value.Contains('\r');
        return needsQuotes
            ? string.Concat("\"", value.Replace("\"", "\"\"", StringComparison.Ordinal), "\"")
            : value;
    }

    private static void WriteProperties(TextWriter writer, TdmsDocument document, List<TdmsChannel> channels)
    {
        writer.WriteLine("# TestBench.tools TDMS Converter — property header");
        if (document.SourcePath is { } source)
        {
            writer.WriteLine($"# source = {Path.GetFileName(source)}");
        }

        foreach (var property in document.Properties)
        {
            writer.WriteLine($"# file.{property.Key} = {Flatten(property.Value.ToInvariantString())}");
        }

        var groups = channels
            .Select(c => c.GroupName)
            .Distinct(StringComparer.Ordinal)
            .Select(document.FindGroup)
            .OfType<TdmsGroup>();
        foreach (var group in groups)
        {
            foreach (var property in group.Properties)
            {
                writer.WriteLine($"# group[{group.Name}].{property.Key} = {Flatten(property.Value.ToInvariantString())}");
            }
        }

        foreach (var channel in channels)
        {
            var scope = $"# channel[{channel.GroupName}/{channel.Name}]";
            writer.WriteLine($"{scope}.dtype = {TdmsDataTypes.Name(channel.DataType)}");
            writer.WriteLine($"{scope}.samples = {channel.SampleCount.ToString(CultureInfo.InvariantCulture)}");
            foreach (var property in channel.Properties)
            {
                writer.WriteLine($"{scope}.{property.Key} = {Flatten(property.Value.ToInvariantString())}");
            }
        }
    }

    private static string Flatten(string value) => value
        .Replace("\r\n", "\\n", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\r", "\\n", StringComparison.Ordinal);

    /// <summary>
    /// Buffers at most one chunk per channel and emits rows as soon as every channel that
    /// still owes a value has delivered it.
    /// </summary>
    private sealed class CsvRowSink(
        TextWriter writer,
        List<TdmsChannel> channels,
        TdmsChannel? timeChannel,
        TdmsExportRequest request) : TdmsDataSink, IDisposable
    {
        private readonly Dictionary<string, int> _columnByPath = channels
            .Select((c, i) => (c.Path, Index: i))
            .ToDictionary(x => x.Path, x => x.Index, StringComparer.Ordinal);

        private readonly Queue<string>[] _pending = channels.Select(_ => new Queue<string>()).ToArray();
        private readonly long[] _expected = channels.Select(c => c.SampleCount).ToArray();
        private readonly long[] _delivered = new long[channels.Count];
        private readonly StringBuilder _row = new();
        private readonly long _totalRows = channels.Count == 0 ? 0 : channels.Max(c => c.SampleCount);

        private long _rowsWritten;
        private long _lastReported;

        public override void OnSamples(TdmsChannelRef channel, TdmsSampleBuffer samples)
        {
            if (!_columnByPath.TryGetValue(channel.Path, out var column))
            {
                return;
            }

            var queue = _pending[column];
            for (var i = 0; i < samples.Count; i++)
            {
                queue.Enqueue(samples.GetText(i));
            }

            _delivered[column] += samples.Count;
            EmitReadyRows();
        }

        public void Complete()
        {
            // Anything still queued belongs to rows we can now finish: the file is at its end.
            while (_rowsWritten < _totalRows && _pending.Any(q => q.Count > 0))
            {
                WriteRow();
            }

            Report(force: true);
        }

        public void Dispose() => _row.Clear();

        private void EmitReadyRows()
        {
            while (_rowsWritten < _totalRows && RowIsReady())
            {
                WriteRow();
            }

            Report(force: false);
        }

        private bool RowIsReady()
        {
            for (var i = 0; i < _pending.Length; i++)
            {
                if (_expected[i] > _rowsWritten && _pending[i].Count == 0)
                {
                    return false;
                }
            }

            return true;
        }

        private void WriteRow()
        {
            _row.Clear();
            if (timeChannel is not null && timeChannel.TryGetRelativeTime(_rowsWritten, out var seconds))
            {
                _row.Append(seconds.ToString(CultureInfo.InvariantCulture)).Append(request.Delimiter);
            }
            else if (timeChannel is not null)
            {
                _row.Append(request.Delimiter);
            }

            for (var i = 0; i < _pending.Length; i++)
            {
                if (i > 0)
                {
                    _row.Append(request.Delimiter);
                }

                if (_pending[i].TryDequeue(out var value))
                {
                    _row.Append(Escape(value, request.Delimiter));
                }
            }

            writer.WriteLine(_row.ToString());
            _rowsWritten++;
        }

        private void Report(bool force)
        {
            if (request.Progress is null)
            {
                return;
            }

            if (!force && _rowsWritten - _lastReported < ProgressEveryRows)
            {
                return;
            }

            _lastReported = _rowsWritten;
            request.Progress.Report(new TdmsExportProgress(_rowsWritten, _totalRows));
        }
    }
}

/// <summary>Plain CSV: a header row of <c>group/channel</c> names and one column per channel.</summary>
public sealed class CsvExporter : CsvExporterBase
{
    /// <inheritdoc />
    public override string Id => "csv";

    /// <inheritdoc />
    public override string DisplayNameKey => "FormatCsv";

    /// <inheritdoc />
    protected override bool WritePropertyHeader => false;
}

/// <summary>
/// CSV preceded by every file, group and channel property as <c>#</c> comment lines — the
/// part of a TDMS file a plain CSV would silently drop.
/// </summary>
public sealed class CsvWithPropertiesExporter : CsvExporterBase
{
    /// <inheritdoc />
    public override string Id => "csv-properties";

    /// <inheritdoc />
    public override string DisplayNameKey => "FormatCsvProperties";

    /// <inheritdoc />
    protected override bool WritePropertyHeader => true;
}
