using System.IO.Compression;
using System.Xml.Linq;
using Tdms.Core.Export;
using Tdms.Core.Model;

namespace Tdms.Core.Tests;

/// <summary>
/// The xlsx writer is hand-rolled, so these check the things a spreadsheet library would
/// otherwise guarantee: that the package has the parts Excel requires, that every part is
/// well-formed XML, and that numbers land in numeric cells rather than as text.
/// </summary>
public sealed class XlsxExportTests : IDisposable
{
    private static readonly XNamespace Main =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "tdms-xlsx-" + Guid.NewGuid().ToString("N"));

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
        first.AddObject(TdmsPath.Root);
        first.AddChannel(ChannelA, TdmsDataType.F64, [1.0, 2.5]);
        first.AddChannel(ChannelB, TdmsDataType.I32, [10, 20]);

        var second = writer.AddSegment();
        second.AddChannel(ChannelA, TdmsDataType.F64, [-3.0]);

        return writer.WriteTo(_directory, "ragged");
    }

    private byte[] Export(string source, params string[] channels)
    {
        using var buffer = new MemoryStream();
        TdmsExporters.Xlsx.Export(
            new TdmsExportRequest { SourcePath = source, ChannelPaths = channels },
            buffer);
        return buffer.ToArray();
    }

    private static XDocument Part(byte[] xlsx, string path)
    {
        using var zip = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read);
        var entry = zip.GetEntry(path);
        Assert.NotNull(entry);
        using var stream = entry!.Open();
        return XDocument.Load(stream);
    }

    [Fact]
    public void PackageHasTheFourPartsExcelRequires()
    {
        var xlsx = Export(WriteRaggedFile(), ChannelA, ChannelB);

        using var zip = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read);
        var names = zip.Entries.Select(e => e.FullName).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("[Content_Types].xml", names);
        Assert.Contains("_rels/.rels", names);
        Assert.Contains("xl/workbook.xml", names);
        Assert.Contains("xl/_rels/workbook.xml.rels", names);
        Assert.Contains("xl/worksheets/sheet1.xml", names);
    }

    [Fact]
    public void EveryPartIsWellFormedXml()
    {
        var xlsx = Export(WriteRaggedFile(), ChannelA, ChannelB);

        using var zip = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read);
        foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.Ordinal)
                                                     || e.FullName.EndsWith(".rels", StringComparison.Ordinal)))
        {
            using var stream = entry.Open();
            var parsed = Record.Exception(() => XDocument.Load(stream));
            Assert.Null(parsed);
        }
    }

    [Fact]
    public void HeaderNamesTheChannelsAndRowsCarryTheValues()
    {
        var xlsx = Export(WriteRaggedFile(), ChannelA, ChannelB);
        var rows = Part(xlsx, "xl/worksheets/sheet1.xml").Descendants(Main + "row").ToList();

        // Header + three rows; channel b is one short and leaves its cell out.
        Assert.Equal(4, rows.Count);

        var header = rows[0].Descendants(Main + "t").Select(t => t.Value).ToList();
        Assert.Equal(["M/a", "M/b"], header);

        double Value(XElement row, int column) =>
            double.Parse(
                row.Descendants(Main + "c")
                    .First(c => c.Attribute("r")!.Value.StartsWith(XlsxExporter.ColumnName(column), StringComparison.Ordinal))
                    .Element(Main + "v")!.Value,
                System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(1.0, Value(rows[1], 0));
        Assert.Equal(10, Value(rows[1], 1));
        Assert.Equal(2.5, Value(rows[2], 0));
        Assert.Equal(-3.0, Value(rows[3], 0));

        // The short channel contributes no cell to the last row rather than a shifted one.
        Assert.Single(rows[3].Descendants(Main + "c"));
    }

    [Fact]
    public void NumbersAreNumericCellsNotText()
    {
        var xlsx = Export(WriteRaggedFile(), ChannelA);
        var rows = Part(xlsx, "xl/worksheets/sheet1.xml").Descendants(Main + "row").ToList();

        // A numeric cell has <v> and no t="inlineStr"; getting this wrong is the difference
        // between a spreadsheet you can sum and one you have to convert first.
        var cell = rows[1].Descendants(Main + "c").Single();
        Assert.Null(cell.Attribute("t"));
        Assert.NotNull(cell.Element(Main + "v"));
        Assert.Empty(cell.Descendants(Main + "is"));

        // The header is the opposite.
        var headerCell = rows[0].Descendants(Main + "c").Single();
        Assert.Equal("inlineStr", headerCell.Attribute("t")!.Value);
    }

    [Fact]
    public void ChannelNameWithMarkupDoesNotBreakThePackage()
    {
        var writer = new TdmsTestWriter();
        var segment = writer.AddSegment();
        segment.AddObject(TdmsPath.Root);
        var hostile = TdmsPath.ForChannel("M", "a<b & \"c\"");
        segment.AddChannel(hostile, TdmsDataType.F64, [1.0]);
        var source = writer.WriteTo(_directory, "hostile");

        var xlsx = Export(source, hostile);
        var header = Part(xlsx, "xl/worksheets/sheet1.xml")
            .Descendants(Main + "t").Select(t => t.Value).Single();

        Assert.Equal("M/a<b & \"c\"", header);
    }

    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(27, "AB")]
    [InlineData(51, "AZ")]
    [InlineData(52, "BA")]
    [InlineData(701, "ZZ")]
    [InlineData(702, "AAA")]
    [InlineData(16383, "XFD")]
    public void ColumnNamesMatchExcel(int index, string expected) =>
        Assert.Equal(expected, XlsxExporter.ColumnName(index));

    [Fact]
    public void SheetLimitsAreStatedRatherThanSilentlyTruncated()
    {
        // The refusal text has to name the limit and point at CSV; a workbook cut off at a
        // million rows looks complete and is missing data.
        Assert.Equal(1_048_576, XlsxExporter.MaxRows);
        Assert.Equal(16_384, XlsxExporter.MaxColumns);
    }
}
