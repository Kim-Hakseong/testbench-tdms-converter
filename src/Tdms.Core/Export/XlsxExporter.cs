using System.Globalization;
using System.IO.Compression;
using System.Text;
using Tdms.Core.Model;

namespace Tdms.Core.Export;

/// <summary>
/// Writes the selected channels as a real .xlsx workbook.
///
/// Hand-written rather than taken from a package, for the same reason the TDMS reader is:
/// Tdms.Core has no dependencies and an export path is not the place to acquire one. An
/// xlsx is a zip of a few XML parts, and the subset needed for a numeric table is small.
/// Writing it ourselves also keeps the export streaming — the usual spreadsheet libraries
/// build the whole sheet in memory, which is the opposite of what a multi-gigabyte
/// measurement file needs.
///
/// Numbers are written as numbers, so Excel treats them as numeric without a conversion
/// step. Text falls back to an inline string; there is no shared-string table, which costs
/// a little size on repetitive text and buys the ability to write a row and forget it.
/// </summary>
public sealed class XlsxExporter : ITdmsExporter
{
    /// <summary>Excel's hard row limit, header included.</summary>
    public const long MaxRows = 1_048_576;

    /// <summary>Excel's hard column limit.</summary>
    public const int MaxColumns = 16_384;

    /// <inheritdoc />
    public string Id => "xlsx";

    /// <inheritdoc />
    public string DisplayNameKey => "FormatXlsx";

    /// <inheritdoc />
    public string FileExtension => ".xlsx";

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

        var timeChannel = request.IncludeWaveformTimeColumn
            ? channels.FirstOrDefault(c => c.HasWaveformTiming)
            : null;

        var columns = channels.Count + (timeChannel is not null ? 1 : 0);
        if (columns > MaxColumns)
        {
            throw new TdmsException(
                $"An xlsx sheet holds {MaxColumns} columns and this export needs {columns}. " +
                "Export fewer channels, or use CSV, which has no column limit.");
        }

        // The header takes one of the rows, so the data can only fill the rest. Checked
        // before writing anything: a workbook truncated at a million rows would look
        // complete and be missing data, which is worse than refusing.
        var dataRows = channels.Count == 0 ? 0 : channels.Max(c => c.SampleCount);
        if (dataRows + 1 > MaxRows)
        {
            throw new TdmsException(
                $"An xlsx sheet holds {MaxRows:N0} rows including the header, and this export " +
                $"needs {dataRows + 1:N0}. Use CSV, which has no row limit.");
        }

        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        WriteEntry(zip, "[Content_Types].xml", ContentTypes);
        WriteEntry(zip, "_rels/.rels", RootRels);
        WriteEntry(zip, "xl/workbook.xml", Workbook);
        WriteEntry(zip, "xl/_rels/workbook.xml.rels", WorkbookRels);

        var sheet = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal);
        using var stream = sheet.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), 64 * 1024)
        {
            NewLine = "\n",
        };

        writer.Write(
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

        WriteHeaderRow(writer, channels, timeChannel);

        using var sink = new XlsxRowSink(writer, channels, timeChannel, request.Progress);
        TdmsFileReader.StreamData(
            request.SourcePath,
            sink,
            channels.Select(c => c.Path).ToList(),
            cancellationToken);
        sink.Complete();

        writer.Write("</sheetData></worksheet>");
        writer.Flush();
    }

    /// <summary>Column reference for a zero-based index: 0 → A, 26 → AA.</summary>
    /// <param name="index">Zero-based column index.</param>
    /// <returns>The letters Excel uses for that column.</returns>
    public static string ColumnName(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        Span<char> buffer = stackalloc char[4];
        var at = buffer.Length;
        var n = index;
        do
        {
            buffer[--at] = (char)('A' + (n % 26));
            n = (n / 26) - 1;
        }
        while (n >= 0);

        return new string(buffer[at..]);
    }

    private static void WriteHeaderRow(TextWriter writer, List<TdmsChannel> channels, TdmsChannel? timeChannel)
    {
        writer.Write("<row r=\"1\">");
        var column = 0;
        if (timeChannel is not null)
        {
            WriteInlineString(writer, column++, 1, CsvExporterBase.TimeColumnHeader);
        }

        foreach (var channel in channels)
        {
            WriteInlineString(writer, column++, 1, $"{channel.GroupName}/{channel.Name}");
        }

        writer.Write("</row>");
    }

    private static void WriteInlineString(TextWriter writer, int column, long row, string text)
    {
        writer.Write("<c r=\"");
        writer.Write(ColumnName(column));
        writer.Write(row.ToString(CultureInfo.InvariantCulture));
        writer.Write("\" t=\"inlineStr\"><is><t xml:space=\"preserve\">");
        WriteEscaped(writer, text);
        writer.Write("</t></is></c>");
    }

    /// <summary>
    /// XML rejects a handful of control characters outright, and a TDMS channel name is
    /// free-form text that can contain them. Escaping the five markup characters and
    /// dropping the rest is what keeps a hostile name from producing a workbook Excel
    /// refuses to open.
    /// </summary>
    private static void WriteEscaped(TextWriter writer, string text)
    {
        foreach (var ch in text)
        {
            switch (ch)
            {
                case '<': writer.Write("&lt;"); break;
                case '>': writer.Write("&gt;"); break;
                case '&': writer.Write("&amp;"); break;
                case '"': writer.Write("&quot;"); break;
                case '\'': writer.Write("&apos;"); break;
                default:
                    if (ch >= 0x20 || ch is '\t' or '\n' or '\r')
                    {
                        writer.Write(ch);
                    }

                    break;
            }
        }
    }

    private static void WriteEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private const string ContentTypes =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
        "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
        "</Types>";

    private const string RootRels =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
        "</Relationships>";

    private const string Workbook =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
        "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
        "<sheets><sheet name=\"Data\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>";

    private const string WorkbookRels =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
        "</Relationships>";

    /// <summary>Writes assembled rows as sheet XML.</summary>
    private sealed class XlsxRowSink(
        TextWriter writer,
        IReadOnlyList<TdmsChannel> channels,
        TdmsChannel? timeChannel,
        IProgress<TdmsExportProgress>? progress)
        : ChannelRowSink(channels, timeChannel, progress)
    {
        protected override void WriteRow(long rowIndex, double? time, bool hasTimeColumn, string?[] cells)
        {
            // Sheet rows are 1-based and row 1 is the header.
            var row = rowIndex + 2;
            writer.Write("<row r=\"");
            writer.Write(row.ToString(CultureInfo.InvariantCulture));
            writer.Write("\">");

            var column = 0;
            if (hasTimeColumn)
            {
                if (time is { } seconds)
                {
                    WriteNumber(writer, column, row, seconds.ToString("R", CultureInfo.InvariantCulture));
                }

                column++;
            }

            foreach (var cell in cells)
            {
                if (cell is { Length: > 0 } value)
                {
                    // The reader already produced invariant text for numeric channels, so a
                    // value that parses is written as a number and anything else — a string
                    // channel, a NaN — stays text rather than becoming a broken numeric cell.
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                        && !double.IsNaN(number)
                        && !double.IsInfinity(number))
                    {
                        WriteNumber(writer, column, row, value);
                    }
                    else
                    {
                        WriteInlineString(writer, column, row, value);
                    }
                }

                column++;
            }

            writer.Write("</row>");
        }

        private static void WriteNumber(TextWriter writer, int column, long row, string value)
        {
            writer.Write("<c r=\"");
            writer.Write(ColumnName(column));
            writer.Write(row.ToString(CultureInfo.InvariantCulture));
            writer.Write("\"><v>");
            writer.Write(value);
            writer.Write("</v></c>");
        }
    }
}
