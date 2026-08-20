using Tdms.Core.Model;

namespace Tdms.Core.Export;

/// <summary>
/// Turns the reader's per-channel sample chunks into whole rows.
///
/// Channels arrive independently and in unequal-sized chunks, so a row can only be emitted
/// once every channel that still owes a value for it has delivered one. Ragged channels —
/// one shorter than the rest, which a truncated file produces — leave their cell empty
/// rather than shifting the columns.
///
/// This lived inside the CSV writer until XLSX needed exactly the same alignment. Getting it
/// subtly different in two places is how one format silently disagrees with the other.
/// </summary>
public abstract class ChannelRowSink : TdmsDataSink, IDisposable
{
    private const int ProgressEveryRows = 4096;

    private readonly Dictionary<string, int> _columnByPath;
    private readonly Queue<string>[] _pending;
    private readonly long[] _expected;
    private readonly TdmsChannel? _timeChannel;
    private readonly IProgress<TdmsExportProgress>? _progress;
    private readonly string?[] _cells;

    private long _rowsWritten;
    private long _lastReported;

    /// <summary>Creates a sink for the given channels.</summary>
    /// <param name="channels">Channels in output column order.</param>
    /// <param name="timeChannel">Channel supplying the derived time column, or null for none.</param>
    /// <param name="progress">Optional progress receiver.</param>
    protected ChannelRowSink(
        IReadOnlyList<TdmsChannel> channels,
        TdmsChannel? timeChannel,
        IProgress<TdmsExportProgress>? progress)
    {
        ArgumentNullException.ThrowIfNull(channels);

        _columnByPath = channels
            .Select((c, i) => (c.Path, Index: i))
            .ToDictionary(x => x.Path, x => x.Index, StringComparer.Ordinal);
        _pending = channels.Select(_ => new Queue<string>()).ToArray();
        _expected = channels.Select(c => c.SampleCount).ToArray();
        _cells = new string?[channels.Count];
        _timeChannel = timeChannel;
        _progress = progress;
        TotalRows = channels.Count == 0 ? 0 : channels.Max(c => c.SampleCount);
    }

    /// <summary>Rows the metadata says this export will produce.</summary>
    public long TotalRows { get; }

    /// <summary>Rows emitted so far.</summary>
    public long RowsWritten => _rowsWritten;

    /// <summary>
    /// Receives one assembled row.
    /// </summary>
    /// <param name="rowIndex">Zero-based row number.</param>
    /// <param name="time">
    /// Derived time in seconds, or null when there is no time column or this row has no
    /// timing for it.
    /// </param>
    /// <param name="hasTimeColumn">Whether a time column exists at all, empty cell or not.</param>
    /// <param name="cells">One entry per channel; null where that channel has no sample here.</param>
    protected abstract void WriteRow(long rowIndex, double? time, bool hasTimeColumn, string?[] cells);

    /// <inheritdoc />
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

        while (_rowsWritten < TotalRows && RowIsReady())
        {
            EmitRow();
        }

        Report(force: false);
    }

    /// <summary>Flushes whatever is still queued; the file is at its end.</summary>
    public void Complete()
    {
        while (_rowsWritten < TotalRows && _pending.Any(q => q.Count > 0))
        {
            EmitRow();
        }

        Report(force: true);
    }

    /// <inheritdoc />
    public void Dispose() => GC.SuppressFinalize(this);

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

    private void EmitRow()
    {
        double? time = null;
        var hasTimeColumn = _timeChannel is not null;
        if (_timeChannel is not null && _timeChannel.TryGetRelativeTime(_rowsWritten, out var seconds))
        {
            time = seconds;
        }

        for (var i = 0; i < _pending.Length; i++)
        {
            _cells[i] = _pending[i].TryDequeue(out var value) ? value : null;
        }

        WriteRow(_rowsWritten, time, hasTimeColumn, _cells);
        _rowsWritten++;
    }

    private void Report(bool force)
    {
        if (_progress is null)
        {
            return;
        }

        if (!force && _rowsWritten - _lastReported < ProgressEveryRows)
        {
            return;
        }

        _lastReported = _rowsWritten;
        _progress.Report(new TdmsExportProgress(_rowsWritten, TotalRows));
    }
}
