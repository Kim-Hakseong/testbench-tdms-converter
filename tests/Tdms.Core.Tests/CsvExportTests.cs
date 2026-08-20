using System.Text;
using Tdms.Core.Export;

namespace Tdms.Core.Tests;

/// <summary>Exact-content tests for the two CSV writers.</summary>
public sealed class CsvExportTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "tdms-csv-" + Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static string ChannelA => TdmsPath.ForChannel("M", "a");

    private static string ChannelB => TdmsPath.ForChannel("M", "b");

    /// <summary>Two channels of different length, so rows have to be padded.</summary>
    private string WriteRaggedFile()
    {
        var writer = new TdmsTestWriter();
        var first = writer.AddSegment();
        first.AddObject(TdmsPath.Root, ("title", TdmsPropertyValue.FromString("ragged")));
        first.AddObject(TdmsPath.ForGroup("M"), ("location", TdmsPropertyValue.FromString("bench")));
        first.AddChannel(ChannelA, TdmsDataType.F64, [1.0, 2.5], ("unit_string", TdmsPropertyValue.FromString("V")));
        first.AddChannel(ChannelB, TdmsDataType.I32, [10, 20]);

        var second = writer.AddSegment();
        second.AddChannel(ChannelA, TdmsDataType.F64, [-3.0]);

        return writer.WriteTo(_directory, "ragged");
    }

    private static string Export(ITdmsExporter exporter, TdmsExportRequest request)
    {
        using var output = new MemoryStream();
        exporter.Export(request, output);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    [Fact]
    public void PlainCsvHasOneColumnPerChannelAndPadsRaggedRows()
    {
        var path = WriteRaggedFile();

        var csv = Export(TdmsExporters.Csv, new TdmsExportRequest
        {
            SourcePath = path,
            ChannelPaths = [ChannelA, ChannelB],
        });

        Assert.Equal(
            """
            M/a,M/b
            1,10
            2.5,20
            -3,

            """.ReplaceLineEndings("\n"),
            csv);
    }

    [Fact]
    public void ThePropertyHeaderVariantKeepsWhatCsvWouldThrowAway()
    {
        var path = WriteRaggedFile();

        var csv = Export(TdmsExporters.CsvWithProperties, new TdmsExportRequest
        {
            SourcePath = path,
            ChannelPaths = [ChannelA, ChannelB],
        });

        Assert.Equal(
            """
            # TestBench.tools TDMS Converter — property header
            # source = ragged.tdms
            # file.title = ragged
            # group[M].location = bench
            # channel[M/a].dtype = f64
            # channel[M/a].samples = 3
            # channel[M/a].unit_string = V
            # channel[M/b].dtype = i32
            # channel[M/b].samples = 2
            M/a,M/b
            1,10
            2.5,20
            -3,

            """.ReplaceLineEndings("\n"),
            csv);
    }

    [Fact]
    public void ColumnOrderFollowsTheRequest()
    {
        var path = WriteRaggedFile();

        var csv = Export(TdmsExporters.Csv, new TdmsExportRequest
        {
            SourcePath = path,
            ChannelPaths = [ChannelB, ChannelA],
        });

        Assert.StartsWith("M/b,M/a\n10,1\n", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void ASingleChannelExportsOnItsOwn()
    {
        var path = WriteRaggedFile();

        var csv = Export(TdmsExporters.Csv, new TdmsExportRequest
        {
            SourcePath = path,
            ChannelPaths = [ChannelB],
        });

        Assert.Equal("M/b\n10\n20\n", csv);
    }

    [Fact]
    public void FieldsThatContainTheDelimiterAreQuoted()
    {
        var writer = new TdmsTestWriter();
        writer.AddSegment().AddChannel(
            TdmsPath.ForChannel("g", "notes"),
            TdmsDataType.String,
            ["plain", "has,comma", "has \"quotes\""]);
        var path = writer.WriteTo(_directory, "strings");

        var csv = Export(TdmsExporters.Csv, new TdmsExportRequest
        {
            SourcePath = path,
            ChannelPaths = [TdmsPath.ForChannel("g", "notes")],
        });

        Assert.Equal("g/notes\nplain\n\"has,comma\"\n\"has \"\"quotes\"\"\"\n", csv);
    }

    [Fact]
    public void ADifferentDelimiterChangesBothHeaderAndQuoting()
    {
        var path = WriteRaggedFile();

        var csv = Export(TdmsExporters.Csv, new TdmsExportRequest
        {
            SourcePath = path,
            ChannelPaths = [ChannelA, ChannelB],
            Delimiter = ';',
        });

        Assert.Equal("M/a;M/b\n1;10\n2.5;20\n-3;\n", csv);
    }

    [Fact]
    public void WaveformChannelsGetADerivedTimeColumn()
    {
        var path = SampleFiles.BuildKitchenSink().WriteTo(_directory, "kitchen");

        var csv = Export(TdmsExporters.Csv, new TdmsExportRequest
        {
            SourcePath = path,
            ChannelPaths = [SampleFiles.Voltage, SampleFiles.Counter],
        });

        var lines = csv.Split('\n');
        Assert.Equal("Time (s),Analog/Voltage,Analog/Counter", lines[0]);
        Assert.Equal("0,0,-1000", lines[1]);
        Assert.Equal("0.001,0.5,-963", lines[2]);
        Assert.Equal("0.002,1,-926", lines[3]);

        // The voltage channel is three samples longer than the counter, so its rows are padded.
        Assert.EndsWith(",9,", lines[19], StringComparison.Ordinal);
        Assert.Equal(string.Empty, lines[20]);
    }

    [Fact]
    public void TheTimeColumnCanBeTurnedOff()
    {
        var path = SampleFiles.BuildKitchenSink().WriteTo(_directory, "kitchen");

        var csv = Export(TdmsExporters.Csv, new TdmsExportRequest
        {
            SourcePath = path,
            ChannelPaths = [SampleFiles.Voltage],
            IncludeWaveformTimeColumn = false,
        });

        Assert.StartsWith("Analog/Voltage\n0\n0.5\n", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryTypeIsWrittenInAnInvariantForm()
    {
        var path = SampleFiles.BuildKitchenSink().WriteTo(_directory, "kitchen");

        var csv = Export(TdmsExporters.Csv, new TdmsExportRequest
        {
            SourcePath = path,
            ChannelPaths = [SampleFiles.Current, SampleFiles.Enabled, SampleFiles.Ticks, SampleFiles.Stamp],
            IncludeWaveformTimeColumn = false,
        });

        var lines = csv.Split('\n');
        Assert.Equal("Analog/Current,Digital/Enabled,Digital/Ticks,Digital/Stamp", lines[0]);
        Assert.Equal("1.25,1,9007199254740993,2017-06-11T04:26:40.0000000Z", lines[1]);
        Assert.Equal("1.375,0,9007199254740994,2017-06-11T04:26:41.0542101Z", lines[2]);
    }

    [Fact]
    public void ProgressIsReportedUpToTheRowCount()
    {
        var path = SampleFiles.BuildKitchenSink().WriteTo(_directory, "kitchen");
        var reports = new List<TdmsExportProgress>();

        Export(TdmsExporters.Csv, new TdmsExportRequest
        {
            SourcePath = path,
            ChannelPaths = [SampleFiles.Voltage],
            Progress = new SyncProgress(reports.Add),
        });

        Assert.NotEmpty(reports);
        Assert.Equal(19, reports[^1].RowsWritten);
        Assert.Equal(19, reports[^1].TotalRows);
        Assert.Equal(1.0, reports[^1].Fraction);
    }

    [Fact]
    public void SuppliedMetadataIsReusedInsteadOfRescanning()
    {
        var path = WriteRaggedFile();
        var metadata = TdmsFileReader.ReadMetadata(path);

        var csv = Export(TdmsExporters.Csv, new TdmsExportRequest
        {
            SourcePath = path,
            ChannelPaths = [ChannelA],
            Metadata = metadata,
        });

        Assert.Equal("M/a\n1\n2.5\n-3\n", csv);
    }

    [Fact]
    public void AnUnknownChannelIsRefused()
    {
        var path = WriteRaggedFile();

        var error = Assert.Throws<TdmsException>(() => Export(TdmsExporters.Csv, new TdmsExportRequest
        {
            SourcePath = path,
            ChannelPaths = [TdmsPath.ForChannel("M", "nope")],
        }));

        Assert.Contains("not present", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportingNothingIsRefused()
    {
        var path = WriteRaggedFile();

        Assert.Throws<TdmsException>(() => Export(TdmsExporters.Csv, new TdmsExportRequest
        {
            SourcePath = path,
            ChannelPaths = [],
        }));
    }

    [Fact]
    public void AnExportCanBeCancelled()
    {
        var path = SampleFiles.BuildKitchenSink().WriteTo(_directory, "kitchen");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        using var output = new MemoryStream();

        Assert.Throws<OperationCanceledException>(() => TdmsExporters.Csv.Export(
            new TdmsExportRequest { SourcePath = path, ChannelPaths = [SampleFiles.Voltage] },
            output,
            cancellation.Token));
    }

    [Fact]
    public void TheExporterRegistryExposesEveryFormat()
    {
        Assert.Equal(3, TdmsExporters.All.Count);
        Assert.Same(TdmsExporters.Csv, TdmsExporters.Find("csv"));
        Assert.Same(TdmsExporters.CsvWithProperties, TdmsExporters.Find("csv-properties"));
        Assert.Same(TdmsExporters.Xlsx, TdmsExporters.Find("xlsx"));
        Assert.Null(TdmsExporters.Find("hdf5"));

        // Ids and extensions have to stay distinct: the id is persisted in settings and the
        // extension picks the save dialog's filter.
        Assert.Equal(TdmsExporters.All.Count, TdmsExporters.All.Select(e => e.Id).Distinct().Count());
        Assert.Equal(".csv", TdmsExporters.Csv.FileExtension);
        Assert.Equal(".csv", TdmsExporters.CsvWithProperties.FileExtension);
        Assert.Equal(".xlsx", TdmsExporters.Xlsx.FileExtension);
    }

    /// <summary>Reports on the calling thread, unlike <see cref="Progress{T}"/>.</summary>
    private sealed class SyncProgress(Action<TdmsExportProgress> handler) : IProgress<TdmsExportProgress>
    {
        public void Report(TdmsExportProgress value) => handler(value);
    }
}
