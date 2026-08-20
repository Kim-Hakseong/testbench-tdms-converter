using Tdms.Core.Model;

namespace Tdms.Core.Export;

/// <summary>Progress of a running export.</summary>
/// <param name="RowsWritten">Rows written so far.</param>
/// <param name="TotalRows">Rows expected in total, from the metadata scan.</param>
public readonly record struct TdmsExportProgress(long RowsWritten, long TotalRows)
{
    /// <summary>Completed fraction in [0, 1].</summary>
    public double Fraction => TotalRows <= 0 ? 0 : Math.Clamp(RowsWritten / (double)TotalRows, 0, 1);
}

/// <summary>What to export and how.</summary>
public sealed class TdmsExportRequest
{
    /// <summary>Path of the source <c>.tdms</c> file. The data is streamed from it, never buffered whole.</summary>
    public required string SourcePath { get; init; }

    /// <summary>Object paths of the channels to export, in output column order.</summary>
    public required IReadOnlyList<string> ChannelPaths { get; init; }

    /// <summary>Metadata of the source file. Supplying it avoids a second scan.</summary>
    public TdmsDocument? Metadata { get; init; }

    /// <summary>Field separator for text formats.</summary>
    public char Delimiter { get; init; } = ',';

    /// <summary>
    /// Emit a leading time column derived from <c>wf_start_offset</c> and <c>wf_increment</c>
    /// when at least one exported channel carries waveform timing.
    /// </summary>
    public bool IncludeWaveformTimeColumn { get; init; } = true;

    /// <summary>Optional progress receiver.</summary>
    public IProgress<TdmsExportProgress>? Progress { get; init; }
}

/// <summary>
/// A converter from TDMS to some other format. CSV and xlsx ship today; HDF5 or Parquet
/// writers plug in behind the same interface.
/// </summary>
public interface ITdmsExporter
{
    /// <summary>Stable identifier used in settings and tests.</summary>
    string Id { get; }

    /// <summary>Localisation key of the human readable name.</summary>
    string DisplayNameKey { get; }

    /// <summary>File extension including the dot, for example <c>.csv</c>.</summary>
    string FileExtension { get; }

    /// <summary>Streams the requested channels into <paramref name="output"/>.</summary>
    /// <param name="request">What to export.</param>
    /// <param name="output">Destination stream; left open.</param>
    /// <param name="cancellationToken">Cancels the export.</param>
    void Export(TdmsExportRequest request, Stream output, CancellationToken cancellationToken = default);
}

/// <summary>The exporters this build ships with.</summary>
public static class TdmsExporters
{
    /// <summary>Plain CSV: one column per channel.</summary>
    public static ITdmsExporter Csv { get; } = new CsvExporter();

    /// <summary>CSV preceded by every TDMS property as <c>#</c> comment lines.</summary>
    public static ITdmsExporter CsvWithProperties { get; } = new CsvWithPropertiesExporter();

    /// <summary>A real .xlsx workbook: one sheet, numbers written as numbers.</summary>
    public static ITdmsExporter Xlsx { get; } = new XlsxExporter();

    /// <summary>Every available exporter, in menu order.</summary>
    public static IReadOnlyList<ITdmsExporter> All { get; } = [Csv, CsvWithProperties, Xlsx];

    /// <summary>Looks an exporter up by <see cref="ITdmsExporter.Id"/>.</summary>
    /// <param name="id">Exporter id.</param>
    /// <returns>The exporter, or <see langword="null"/>.</returns>
    public static ITdmsExporter? Find(string id) =>
        All.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal));
}
