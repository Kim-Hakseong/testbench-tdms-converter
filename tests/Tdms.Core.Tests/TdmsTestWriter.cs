using System.Text;

namespace Tdms.Core.Tests;

/// <summary>
/// A spec-conformant TDMS writer that exists only so the tests can round-trip: write a file
/// whose contents are known exactly, read it back with the production parser, compare. It is
/// deliberately independent of the parser — it shares no code with it.
/// </summary>
public sealed class TdmsTestWriter
{
    private const uint TocMetaData = 1u << 1;
    private const uint TocNewObjectList = 1u << 2;
    private const uint TocRawData = 1u << 3;

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Segments to write, in order.</summary>
    public List<Segment> Segments { get; } = [];

    /// <summary>Extra ToC bits OR-ed into every lead-in, used to build unsupported files.</summary>
    public uint ExtraToc { get; set; }

    /// <summary>Appends a segment.</summary>
    /// <param name="newObjectList">Set the <c>kTocNewObjList</c> bit.</param>
    /// <param name="includeMetadata">Write a metadata block at all.</param>
    /// <returns>The new segment.</returns>
    public Segment AddSegment(bool newObjectList = true, bool includeMetadata = true)
    {
        var segment = new Segment { NewObjectList = newObjectList, IncludeMetadata = includeMetadata };
        Segments.Add(segment);
        return segment;
    }

    /// <summary>Serialises the <c>.tdms</c> data file.</summary>
    /// <returns>The complete file bytes.</returns>
    public byte[] Build() => Serialise(indexFile: false);

    /// <summary>Serialises the matching <c>.tdms_index</c> sidecar.</summary>
    /// <returns>The complete sidecar bytes.</returns>
    public byte[] BuildIndex() => Serialise(indexFile: true);

    /// <summary>Writes both files next to each other and returns the data file path.</summary>
    /// <param name="directory">Target directory.</param>
    /// <param name="name">File name without extension.</param>
    /// <param name="withIndex">Also write the sidecar.</param>
    /// <returns>Path of the written <c>.tdms</c> file.</returns>
    public string WriteTo(string directory, string name, bool withIndex = false)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name + ".tdms");
        File.WriteAllBytes(path, Build());
        if (withIndex)
        {
            File.WriteAllBytes(Path.Combine(directory, name + ".tdms_index"), BuildIndex());
        }

        return path;
    }

    private byte[] Serialise(bool indexFile)
    {
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Utf8, leaveOpen: true);

        foreach (var segment in Segments)
        {
            var metadata = segment.IncludeMetadata ? BuildMetadata(segment) : [];
            var raw = BuildRawData(segment);

            var toc = ExtraToc;
            if (segment.IncludeMetadata)
            {
                toc |= TocMetaData;
            }

            if (segment.NewObjectList)
            {
                toc |= TocNewObjectList;
            }

            if (raw.Length > 0 || segment.ForceRawDataFlag)
            {
                toc |= TocRawData;
            }

            var emittedRaw = segment.TruncatedRawBytes is { } cut ? raw[..Math.Min(cut, raw.Length)] : raw;

            writer.Write(Encoding.ASCII.GetBytes(indexFile ? "TDSh" : "TDSm"));
            writer.Write(toc);
            writer.Write(4713u);
            writer.Write(segment.TruncatedRawBytes is null
                ? (ulong)(metadata.Length + raw.Length)
                : ulong.MaxValue);
            writer.Write((ulong)metadata.Length);
            writer.Write(metadata);
            if (!indexFile)
            {
                writer.Write(emittedRaw);
            }
        }

        writer.Flush();
        return output.ToArray();
    }

    private static byte[] BuildMetadata(Segment segment)
    {
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Utf8, leaveOpen: true);

        writer.Write((uint)segment.Objects.Count);
        foreach (var obj in segment.Objects)
        {
            WriteString(writer, obj.Path);

            if (obj.DaqmxIndex)
            {
                writer.Write(0x69120000u);
            }
            else if (obj.ReuseIndex)
            {
                writer.Write(0x00000000u);
            }
            else if (obj.DataType is not { } type)
            {
                writer.Write(0xFFFFFFFFu);
            }
            else if (type == TdmsDataType.String)
            {
                var count = obj.Chunks[0].Count;
                writer.Write(28u);
                writer.Write((uint)type);
                writer.Write(1u);
                writer.Write((ulong)count);
                writer.Write((ulong)EncodeValues(type, obj.Chunks[0]).Length);
            }
            else
            {
                writer.Write(20u);
                writer.Write((uint)type);
                writer.Write(1u);
                writer.Write((ulong)obj.Chunks[0].Count);
            }

            writer.Write((uint)obj.Properties.Count);
            foreach (var (name, value) in obj.Properties)
            {
                WriteString(writer, name);
                writer.Write((uint)value.DataType);
                WriteScalar(writer, value.DataType, value.Value!);
            }
        }

        writer.Flush();
        return output.ToArray();
    }

    private static byte[] BuildRawData(Segment segment)
    {
        var withData = segment.Objects.Where(o => o.Chunks.Count > 0).ToList();
        if (withData.Count == 0)
        {
            return [];
        }

        var chunkCount = withData[0].Chunks.Count;
        if (withData.Any(o => o.Chunks.Count != chunkCount))
        {
            throw new InvalidOperationException("Every raw object in a segment must have the same chunk count.");
        }

        using var output = new MemoryStream();
        for (var chunk = 0; chunk < chunkCount; chunk++)
        {
            foreach (var obj in withData)
            {
                var bytes = EncodeValues(obj.DataType!.Value, obj.Chunks[chunk]);
                output.Write(bytes, 0, bytes.Length);
            }
        }

        return output.ToArray();
    }

    private static byte[] EncodeValues(TdmsDataType type, IReadOnlyList<object> values)
    {
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Utf8, leaveOpen: true);

        if (type == TdmsDataType.String)
        {
            var payloads = values.Select(v => Utf8.GetBytes((string)v)).ToList();
            uint end = 0;
            foreach (var payload in payloads)
            {
                end += (uint)payload.Length;
                writer.Write(end);
            }

            foreach (var payload in payloads)
            {
                writer.Write(payload);
            }
        }
        else
        {
            foreach (var value in values)
            {
                WriteScalar(writer, type, value);
            }
        }

        writer.Flush();
        return output.ToArray();
    }

    private static void WriteScalar(BinaryWriter writer, TdmsDataType type, object value)
    {
        switch (type)
        {
            case TdmsDataType.I8:
                writer.Write(Convert.ToSByte(value, System.Globalization.CultureInfo.InvariantCulture));
                break;
            case TdmsDataType.U8:
                writer.Write(Convert.ToByte(value, System.Globalization.CultureInfo.InvariantCulture));
                break;
            case TdmsDataType.I16:
                writer.Write(Convert.ToInt16(value, System.Globalization.CultureInfo.InvariantCulture));
                break;
            case TdmsDataType.U16:
                writer.Write(Convert.ToUInt16(value, System.Globalization.CultureInfo.InvariantCulture));
                break;
            case TdmsDataType.I32:
                writer.Write(Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture));
                break;
            case TdmsDataType.U32:
                writer.Write(Convert.ToUInt32(value, System.Globalization.CultureInfo.InvariantCulture));
                break;
            case TdmsDataType.I64:
                writer.Write(Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
                break;
            case TdmsDataType.U64:
                writer.Write(Convert.ToUInt64(value, System.Globalization.CultureInfo.InvariantCulture));
                break;
            case TdmsDataType.F32:
                writer.Write(Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture));
                break;
            case TdmsDataType.F64:
                writer.Write(Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture));
                break;
            case TdmsDataType.Boolean:
                writer.Write((byte)(Convert.ToBoolean(value, System.Globalization.CultureInfo.InvariantCulture) ? 1 : 0));
                break;
            case TdmsDataType.String:
                WriteString(writer, (string)value);
                break;
            case TdmsDataType.Timestamp:
            {
                var timestamp = (TdmsTimestamp)value;
                writer.Write(timestamp.Fractions);
                writer.Write(timestamp.Seconds);
                break;
            }

            default:
                throw new NotSupportedException($"The test writer cannot write {type}.");
        }
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        var bytes = Utf8.GetBytes(value);
        writer.Write((uint)bytes.Length);
        writer.Write(bytes);
    }

    /// <summary>One TDMS segment.</summary>
    public sealed class Segment
    {
        /// <summary>Objects listed in this segment's metadata.</summary>
        public List<WriterObject> Objects { get; } = [];

        /// <summary>Whether the <c>kTocNewObjList</c> bit is set.</summary>
        public bool NewObjectList { get; set; } = true;

        /// <summary>Whether a metadata block is written at all.</summary>
        public bool IncludeMetadata { get; set; } = true;

        /// <summary>Emit the <c>kTocRawData</c> bit even when there is no raw data.</summary>
        public bool ForceRawDataFlag { get; set; }

        /// <summary>
        /// When set, the lead-in carries the 0xFFFFFFFF… "next segment unknown" marker and only
        /// this many raw bytes are written — a file killed mid-write.
        /// </summary>
        public int? TruncatedRawBytes { get; set; }

        /// <summary>Adds an object that carries properties but no raw data.</summary>
        /// <param name="path">TDMS object path.</param>
        /// <param name="properties">Properties to write.</param>
        /// <returns>The object, for chaining.</returns>
        public WriterObject AddObject(string path, params (string Name, TdmsPropertyValue Value)[] properties)
        {
            var obj = new WriterObject { Path = path };
            obj.Properties.AddRange(properties);
            Objects.Add(obj);
            return obj;
        }

        /// <summary>Adds a channel with one chunk of raw data.</summary>
        /// <param name="path">TDMS object path.</param>
        /// <param name="type">Raw data type.</param>
        /// <param name="values">Sample values, boxed.</param>
        /// <param name="properties">Properties to write.</param>
        /// <returns>The object, for chaining.</returns>
        public WriterObject AddChannel(
            string path,
            TdmsDataType type,
            IReadOnlyList<object> values,
            params (string Name, TdmsPropertyValue Value)[] properties)
        {
            var obj = new WriterObject { Path = path, DataType = type };
            obj.Chunks.Add(values);
            obj.Properties.AddRange(properties);
            Objects.Add(obj);
            return obj;
        }

        /// <summary>Adds a channel whose raw data repeats over several chunks in one segment.</summary>
        /// <param name="path">TDMS object path.</param>
        /// <param name="type">Raw data type.</param>
        /// <param name="chunks">One value list per chunk; all must have the same length.</param>
        /// <returns>The object, for chaining.</returns>
        public WriterObject AddChunkedChannel(
            string path,
            TdmsDataType type,
            IReadOnlyList<IReadOnlyList<object>> chunks)
        {
            var obj = new WriterObject { Path = path, DataType = type };
            obj.Chunks.AddRange(chunks);
            Objects.Add(obj);
            return obj;
        }

        /// <summary>Adds a channel that reuses the raw index declared in an earlier segment.</summary>
        /// <param name="path">TDMS object path.</param>
        /// <param name="type">Raw data type, used to encode the values.</param>
        /// <param name="values">Sample values, boxed.</param>
        /// <returns>The object, for chaining.</returns>
        public WriterObject AddIncrementalChannel(string path, TdmsDataType type, IReadOnlyList<object> values)
        {
            var obj = new WriterObject { Path = path, DataType = type, ReuseIndex = true };
            obj.Chunks.Add(values);
            Objects.Add(obj);
            return obj;
        }
    }

    /// <summary>One object inside a segment.</summary>
    public sealed class WriterObject
    {
        /// <summary>TDMS object path.</summary>
        public required string Path { get; init; }

        /// <summary>Raw data type, or <see langword="null"/> when the object has no raw data.</summary>
        public TdmsDataType? DataType { get; init; }

        /// <summary>Write raw index header 0x00000000 (same index as the previous segment).</summary>
        public bool ReuseIndex { get; init; }

        /// <summary>Write the DAQmx format-changing-scaler raw index header, which is unsupported.</summary>
        public bool DaqmxIndex { get; init; }

        /// <summary>Raw values, one list per chunk.</summary>
        public List<IReadOnlyList<object>> Chunks { get; } = [];

        /// <summary>Properties, in write order.</summary>
        public List<(string Name, TdmsPropertyValue Value)> Properties { get; } = [];
    }
}
