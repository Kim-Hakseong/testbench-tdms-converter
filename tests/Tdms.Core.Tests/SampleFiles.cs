namespace Tdms.Core.Tests;

/// <summary>
/// Builds the files the round-trip tests use. Every expected value is produced here, so the
/// assertions compare the parser against an independently generated truth.
/// </summary>
public static class SampleFiles
{
    /// <summary>Group holding the analog channels.</summary>
    public const string AnalogGroup = "Analog";

    /// <summary>Group holding the digital and annotation channels.</summary>
    public const string DigitalGroup = "Digital";

    /// <summary>Sample interval written into <c>wf_increment</c>.</summary>
    public const double WaveformIncrement = 0.001;

    /// <summary>Start instant written into <c>wf_start_time</c>.</summary>
    public static TdmsTimestamp WaveformStart { get; } =
        TdmsTimestamp.FromDateTimeOffset(new DateTimeOffset(2026, 7, 28, 9, 30, 0, TimeSpan.Zero));

    /// <summary>Expected samples of every channel of the kitchen-sink file, keyed by object path.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<object>> Expected { get; } = BuildExpected();

    /// <summary>Channel types of the kitchen-sink file, keyed by object path.</summary>
    public static IReadOnlyDictionary<string, TdmsDataType> Types { get; } = new Dictionary<string, TdmsDataType>
    {
        [Voltage] = TdmsDataType.F64,
        [Current] = TdmsDataType.F32,
        [Counter] = TdmsDataType.I32,
        [Enabled] = TdmsDataType.Boolean,
        [Ticks] = TdmsDataType.U64,
        [Offset] = TdmsDataType.I64,
        [Label] = TdmsDataType.String,
        [Stamp] = TdmsDataType.Timestamp,
    };

    /// <summary>Object path of the f64 waveform channel.</summary>
    public static string Voltage => TdmsPath.ForChannel(AnalogGroup, "Voltage");

    /// <summary>Object path of the f32 channel.</summary>
    public static string Current => TdmsPath.ForChannel(AnalogGroup, "Current");

    /// <summary>Object path of the i32 channel.</summary>
    public static string Counter => TdmsPath.ForChannel(AnalogGroup, "Counter");

    /// <summary>Object path of the bool channel.</summary>
    public static string Enabled => TdmsPath.ForChannel(DigitalGroup, "Enabled");

    /// <summary>Object path of the u64 channel.</summary>
    public static string Ticks => TdmsPath.ForChannel(DigitalGroup, "Ticks");

    /// <summary>Object path of the i64 channel.</summary>
    public static string Offset => TdmsPath.ForChannel(DigitalGroup, "Offset");

    /// <summary>Object path of the string channel.</summary>
    public static string Label => TdmsPath.ForChannel(DigitalGroup, "Label");

    /// <summary>Object path of the timestamp channel.</summary>
    public static string Stamp => TdmsPath.ForChannel(DigitalGroup, "Stamp");

    /// <summary>Object paths in the order the writer lays them out.</summary>
    public static IReadOnlyList<string> ChannelPaths { get; } =
        [Voltage, Current, Counter, Enabled, Ticks, Offset, Label, Stamp];

    /// <summary>
    /// A file that exercises the whole model: two groups, eight data types, properties at file,
    /// group and channel level, and four segments — a full one, an incremental one with two
    /// chunks that reuses the raw index, one with raw data but no metadata at all, and a final
    /// one that declares a new object list holding a single channel (leaving the file ragged).
    /// </summary>
    /// <returns>A writer ready to serialise.</returns>
    public static TdmsTestWriter BuildKitchenSink()
    {
        var writer = new TdmsTestWriter();

        var first = writer.AddSegment();
        first.AddObject(
            TdmsPath.Root,
            ("title", TdmsPropertyValue.FromString("Kitchen sink")),
            ("operator", TdmsPropertyValue.FromString("O'Brien")),
            ("run", TdmsPropertyValue.FromInt32(7)),
            ("archived", TdmsPropertyValue.FromBoolean(false)));
        first.AddObject(
            TdmsPath.ForGroup(AnalogGroup),
            ("location", TdmsPropertyValue.FromString("Cell 3")),
            ("channels", TdmsPropertyValue.FromInt32(3)));
        first.AddObject(
            TdmsPath.ForGroup(DigitalGroup),
            ("location", TdmsPropertyValue.FromString("Cell 4")));

        first.AddChannel(
            Voltage,
            TdmsDataType.F64,
            Slice(Voltage, 0, 4),
            ("unit_string", TdmsPropertyValue.FromString("V")),
            ("gain", TdmsPropertyValue.FromDouble(1.5)),
            ("wf_increment", TdmsPropertyValue.FromDouble(WaveformIncrement)),
            ("wf_start_offset", TdmsPropertyValue.FromDouble(0)),
            ("wf_start_time", TdmsPropertyValue.FromTimestamp(WaveformStart)));
        first.AddChannel(
            Current,
            TdmsDataType.F32,
            Slice(Current, 0, 4),
            ("unit_string", TdmsPropertyValue.FromString("A")));
        first.AddChannel(
            Counter,
            TdmsDataType.I32,
            Slice(Counter, 0, 4),
            ("description", TdmsPropertyValue.FromString("edge count")));
        first.AddChannel(Enabled, TdmsDataType.Boolean, Slice(Enabled, 0, 4));
        first.AddChannel(
            Ticks,
            TdmsDataType.U64,
            Slice(Ticks, 0, 4),
            ("note", TdmsPropertyValue.FromString("beyond 2^53")));
        first.AddChannel(Offset, TdmsDataType.I64, Slice(Offset, 0, 4));
        first.AddChannel(Label, TdmsDataType.String, Slice(Label, 0, 4));
        first.AddChannel(Stamp, TdmsDataType.Timestamp, Slice(Stamp, 0, 4));

        // Incremental segment: no new object list, every raw index reused, two chunks each.
        var incremental = writer.AddSegment(newObjectList: false);
        foreach (var path in ChannelPaths)
        {
            var channel = incremental.AddIncrementalChannel(path, Types[path], Slice(path, 4, 4));
            channel.Chunks.Add(Slice(path, 8, 4));
        }

        // Raw data only: no metadata block at all, the object list persists.
        var rawOnly = writer.AddSegment(newObjectList: false, includeMetadata: false);
        foreach (var path in ChannelPaths)
        {
            rawOnly.AddChannel(path, Types[path], Slice(path, 12, 4));
        }

        // A new object list with a single channel leaves the file ragged.
        var tail = writer.AddSegment();
        tail.AddChannel(Voltage, TdmsDataType.F64, Slice(Voltage, 16, 3));

        return writer;
    }

    /// <summary>Total sample count the kitchen-sink file should report for a channel.</summary>
    /// <param name="path">Channel object path.</param>
    /// <returns>Expected number of samples.</returns>
    public static int ExpectedCount(string path) => Expected[path].Count;

    private static IReadOnlyList<object> Slice(string path, int start, int count) =>
        Expected[path].Skip(start).Take(count).ToList();

    private static Dictionary<string, IReadOnlyList<object>> BuildExpected()
    {
        const int common = 16;
        var map = new Dictionary<string, IReadOnlyList<object>>(StringComparer.Ordinal)
        {
            [TdmsPath.ForChannel(AnalogGroup, "Voltage")] =
                Enumerable.Range(0, common + 3).Select(i => (object)(i * 0.5)).ToList(),
            [TdmsPath.ForChannel(AnalogGroup, "Current")] =
                Enumerable.Range(0, common).Select(i => (object)(1.25f + (i * 0.125f))).ToList(),
            [TdmsPath.ForChannel(AnalogGroup, "Counter")] =
                Enumerable.Range(0, common).Select(i => (object)(-1000 + (i * 37))).ToList(),
            [TdmsPath.ForChannel(DigitalGroup, "Enabled")] =
                Enumerable.Range(0, common).Select(i => (object)(i % 3 == 0)).ToList(),
            [TdmsPath.ForChannel(DigitalGroup, "Ticks")] =
                Enumerable.Range(0, common).Select(i => (object)(9007199254740993UL + (ulong)i)).ToList(),
            [TdmsPath.ForChannel(DigitalGroup, "Offset")] =
                Enumerable.Range(0, common).Select(i => (object)(-9007199254740993L - i)).ToList(),
            // Every label is six bytes long: a TDMS raw index states the byte size of the
            // string block, so chunks that reuse an index must encode to the same size.
            [TdmsPath.ForChannel(DigitalGroup, "Label")] =
                Enumerable.Range(0, common).Select(i => (object)$"ét\"{i:D2}").ToList(),
            [TdmsPath.ForChannel(DigitalGroup, "Stamp")] = Enumerable.Range(0, common)
                .Select(i => (object)new TdmsTimestamp(3_580_000_000L + i, (ulong)i * 1_000_000_000_000_000_000UL))
                .ToList(),
        };
        return map;
    }
}
