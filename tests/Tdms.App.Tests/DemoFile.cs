using Tdms.Core;
using Tdms.Core.Tests;

namespace Tdms.App.Tests;

/// <summary>
/// A realistic measurement file for the UI tests and the README screenshot: two acquisition
/// groups written over two segments, plus an annotation group added later.
/// </summary>
public static class DemoFile
{
    /// <summary>Samples each acquisition channel holds per segment.</summary>
    public const int SamplesPerSegment = 10_000;

    /// <summary>Number of annotation rows.</summary>
    public const int MarkerCount = 240;

    /// <summary>Writes the demo file into a directory.</summary>
    /// <param name="directory">Target directory; created if missing.</param>
    /// <param name="name">File name without extension.</param>
    /// <returns>Path of the written <c>.tdms</c> file.</returns>
    public static string Write(string directory, string name = "endurance-run-42")
    {
        var start = TdmsTimestamp.FromDateTimeOffset(new DateTimeOffset(2026, 7, 27, 22, 15, 0, TimeSpan.Zero));
        var writer = new TdmsTestWriter();

        var first = writer.AddSegment();
        first.AddObject(
            TdmsPath.Root,
            ("name", TdmsPropertyValue.FromString("Endurance run 42")),
            ("operator", TdmsPropertyValue.FromString("bench-2")),
            ("description", TdmsPropertyValue.FromString("Thermal soak, 6 h, ambient 23 °C")),
            ("datetime", TdmsPropertyValue.FromTimestamp(start)));
        first.AddObject(
            TdmsPath.ForGroup("Thermal"),
            ("rack", TdmsPropertyValue.FromString("R3")),
            ("logger", TdmsPropertyValue.FromString("NI 9213")),
            ("cjc", TdmsPropertyValue.FromBoolean(true)));
        first.AddObject(
            TdmsPath.ForGroup("Vibration"),
            ("rack", TdmsPropertyValue.FromString("R3")),
            ("logger", TdmsPropertyValue.FromString("NI 9234")));

        for (var i = 1; i <= 4; i++)
        {
            first.AddChannel(
                TdmsPath.ForChannel("Thermal", $"TC{i}"),
                TdmsDataType.F64,
                Ramp(0, SamplesPerSegment, 23.0 + i, 0.0004),
                ("unit_string", TdmsPropertyValue.FromString("degC")),
                ("NI_ChannelName", TdmsPropertyValue.FromString($"TC{i}")),
                ("thermocouple_type", TdmsPropertyValue.FromString("K")),
                ("wf_start_time", TdmsPropertyValue.FromTimestamp(start)),
                ("wf_increment", TdmsPropertyValue.FromDouble(0.1)),
                ("wf_start_offset", TdmsPropertyValue.FromDouble(0)));
        }

        foreach (var axis in new[] { "X", "Y", "Z" })
        {
            first.AddChannel(
                TdmsPath.ForChannel("Vibration", $"Accel_{axis}"),
                TdmsDataType.F32,
                Wave(0, SamplesPerSegment, axis[0]),
                ("unit_string", TdmsPropertyValue.FromString("g")),
                ("range", TdmsPropertyValue.FromDouble(5.0)),
                ("wf_start_time", TdmsPropertyValue.FromTimestamp(start)),
                ("wf_increment", TdmsPropertyValue.FromDouble(0.001)));
        }

        var second = writer.AddSegment(newObjectList: false);
        for (var i = 1; i <= 4; i++)
        {
            second.AddIncrementalChannel(
                TdmsPath.ForChannel("Thermal", $"TC{i}"),
                TdmsDataType.F64,
                Ramp(SamplesPerSegment, SamplesPerSegment, 23.0 + i, 0.0004));
        }

        foreach (var axis in new[] { "X", "Y", "Z" })
        {
            second.AddIncrementalChannel(
                TdmsPath.ForChannel("Vibration", $"Accel_{axis}"),
                TdmsDataType.F32,
                Wave(SamplesPerSegment, SamplesPerSegment, axis[0]));
        }

        var third = writer.AddSegment();
        third.AddObject(TdmsPath.ForGroup("Events"), ("source", TdmsPropertyValue.FromString("operator log")));
        third.AddChannel(
            TdmsPath.ForChannel("Events", "Marker"),
            TdmsDataType.String,
            Enumerable.Range(0, MarkerCount).Select(i => (object)$"step {i:D3}").ToList());
        third.AddChannel(
            TdmsPath.ForChannel("Events", "Stamp"),
            TdmsDataType.Timestamp,
            Enumerable.Range(0, MarkerCount)
                .Select(i => (object)new TdmsTimestamp(start.Seconds + (i * 90), 0))
                .ToList());

        return writer.WriteTo(directory, name, withIndex: true);
    }

    private static List<object> Ramp(int offset, int count, double baseline, double slope) =>
        Enumerable.Range(offset, count)
            .Select(i => (object)Math.Round(baseline + (i * slope) + (Math.Sin(i / 400.0) * 0.35), 4))
            .ToList();

    private static List<object> Wave(int offset, int count, char axis) =>
        Enumerable.Range(offset, count)
            .Select(i => (object)(float)Math.Round(Math.Sin((i / 30.0) + axis) * 0.8, 4))
            .ToList();
}
