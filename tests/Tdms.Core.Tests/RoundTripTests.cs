using Tdms.Core.Model;

namespace Tdms.Core.Tests;

/// <summary>
/// Writes a file whose every byte is known, reads it back with the production parser and
/// compares the tree, every property and every sample.
/// </summary>
public sealed class RoundTripTests
{
    private static TdmsDocument Parse(byte[] bytes, TdmsReadOptions? options = null) =>
        TdmsFileReader.Read(new MemoryStream(bytes), options ?? new TdmsReadOptions());

    [Fact]
    public void TheGroupAndChannelTreeSurvivesTheRoundTrip()
    {
        var document = Parse(SampleFiles.BuildKitchenSink().Build());

        Assert.Equal([SampleFiles.AnalogGroup, SampleFiles.DigitalGroup], document.Groups.Select(g => g.Name));
        Assert.Equal(
            ["Voltage", "Current", "Counter"],
            document.FindGroup(SampleFiles.AnalogGroup)!.Channels.Select(c => c.Name));
        Assert.Equal(
            ["Enabled", "Ticks", "Offset", "Label", "Stamp"],
            document.FindGroup(SampleFiles.DigitalGroup)!.Channels.Select(c => c.Name));
        Assert.Equal(8, document.ChannelCount);
        Assert.Equal(4, document.SegmentCount);
        Assert.False(document.IsTruncated);
    }

    [Fact]
    public void FilePropertiesSurviveWithTheirTypes()
    {
        var document = Parse(SampleFiles.BuildKitchenSink().Build());

        Assert.Equal(["title", "operator", "run", "archived"], document.Properties.Keys);
        Assert.Equal("Kitchen sink", document.Properties["title"].ToInvariantString());
        Assert.Equal("O'Brien", document.Properties["operator"].ToInvariantString());
        Assert.Equal(TdmsDataType.I32, document.Properties["run"].DataType);
        Assert.Equal("7", document.Properties["run"].ToInvariantString());
        Assert.Equal(TdmsDataType.Boolean, document.Properties["archived"].DataType);
        Assert.Equal(false, document.Properties["archived"].Value);
    }

    [Fact]
    public void GroupPropertiesSurvive()
    {
        var document = Parse(SampleFiles.BuildKitchenSink().Build());

        var analog = document.FindGroup(SampleFiles.AnalogGroup)!;
        Assert.Equal("Cell 3", analog.Properties["location"].ToInvariantString());
        Assert.Equal("3", analog.Properties["channels"].ToInvariantString());
        Assert.Equal("Cell 4", document.FindGroup(SampleFiles.DigitalGroup)!.Properties["location"].ToInvariantString());
    }

    [Fact]
    public void ChannelPropertiesSurviveIncludingWaveformTiming()
    {
        var document = Parse(SampleFiles.BuildKitchenSink().Build());

        var voltage = document.FindChannelByPath(SampleFiles.Voltage)!;
        Assert.Equal("V", voltage.Properties["unit_string"].ToInvariantString());
        Assert.Equal("1.5", voltage.Properties["gain"].ToInvariantString());
        Assert.True(voltage.HasWaveformTiming);
        Assert.Equal(SampleFiles.WaveformIncrement, voltage.WaveformIncrement);
        Assert.Equal(SampleFiles.WaveformStart, voltage.WaveformStartTime);

        Assert.True(voltage.TryGetRelativeTime(3, out var seconds));
        Assert.Equal(0.003, seconds, 12);
        Assert.True(voltage.TryGetAbsoluteTime(1000, out var absolute));
        Assert.Equal(
            SampleFiles.WaveformStart.ToDateTimeOffset().AddSeconds(1),
            absolute);

        Assert.Equal("edge count", document.FindChannelByPath(SampleFiles.Counter)!.Properties["description"].ToInvariantString());
        Assert.False(document.FindChannelByPath(SampleFiles.Current)!.HasWaveformTiming);
    }

    [Fact]
    public void EverySampleOfEveryTypeSurvives()
    {
        var document = Parse(SampleFiles.BuildKitchenSink().Build());

        foreach (var path in SampleFiles.ChannelPaths)
        {
            var channel = document.FindChannelByPath(path);
            Assert.NotNull(channel);
            Assert.Equal(SampleFiles.Types[path], channel!.DataType);

            var expected = SampleFiles.Expected[path];
            Assert.Equal(expected.Count, channel.SampleCount);
            Assert.NotNull(channel.Data);
            Assert.Equal(expected.Count, channel.Data!.Count);

            for (var i = 0; i < expected.Count; i++)
            {
                Assert.Equal(
                    TestFormat.Text(channel.DataType, expected[i]),
                    channel.Data.GetText(i));
            }
        }
    }

    [Fact]
    public void SixtyFourBitIntegersKeepEveryDigit()
    {
        var document = Parse(SampleFiles.BuildKitchenSink().Build());

        // 9007199254740993 is the first integer a double cannot represent.
        Assert.Equal("9007199254740993", document.FindChannelByPath(SampleFiles.Ticks)!.Data!.GetText(0));
        Assert.Equal("-9007199254740993", document.FindChannelByPath(SampleFiles.Offset)!.Data!.GetText(0));
        Assert.Equal(9007199254740993UL, document.FindChannelByPath(SampleFiles.Ticks)!.Data!.GetValue(0));
        Assert.Equal(-9007199254740993L, document.FindChannelByPath(SampleFiles.Offset)!.Data!.GetValue(0));
    }

    [Fact]
    public void BoxedValuesUseTheNaturalClrType()
    {
        var document = Parse(SampleFiles.BuildKitchenSink().Build());

        Assert.IsType<double>(document.FindChannelByPath(SampleFiles.Voltage)!.Data!.GetValue(0));
        Assert.IsType<float>(document.FindChannelByPath(SampleFiles.Current)!.Data!.GetValue(0));
        Assert.IsType<long>(document.FindChannelByPath(SampleFiles.Counter)!.Data!.GetValue(0));
        Assert.IsType<bool>(document.FindChannelByPath(SampleFiles.Enabled)!.Data!.GetValue(0));
        Assert.IsType<string>(document.FindChannelByPath(SampleFiles.Label)!.Data!.GetValue(0));
        Assert.IsType<TdmsTimestamp>(document.FindChannelByPath(SampleFiles.Stamp)!.Data!.GetValue(0));
    }

    [Fact]
    public void IncrementalSegmentsAndRawOnlySegmentsBothContribute()
    {
        var document = Parse(SampleFiles.BuildKitchenSink().Build());

        // 4 (first) + 8 (two chunks reusing the index) + 4 (raw only, no metadata) = 16.
        Assert.Equal(16, document.FindChannelByPath(SampleFiles.Counter)!.SampleCount);

        // The final segment declares a new object list with the voltage channel alone.
        Assert.Equal(19, document.FindChannelByPath(SampleFiles.Voltage)!.SampleCount);
    }

    [Fact]
    public void StringChannelsKeepQuotesAndNonAsciiText()
    {
        var document = Parse(SampleFiles.BuildKitchenSink().Build());

        var label = document.FindChannelByPath(SampleFiles.Label)!;
        Assert.Equal(TdmsDataType.String, label.DataType);
        Assert.Equal("ét\"00", label.Data!.GetText(0));
        Assert.Equal("ét\"15", label.Data.GetText(15));
    }

    [Fact]
    public void ChannelNamesWithQuotesRoundTrip()
    {
        var writer = new TdmsTestWriter();
        var segment = writer.AddSegment();
        segment.AddChannel(
            TdmsPath.ForChannel("O'Hara's rig", "d'Alembert"),
            TdmsDataType.F64,
            [1.0, 2.0]);

        var document = Parse(writer.Build());

        Assert.Equal("O'Hara's rig", document.Groups[0].Name);
        Assert.Equal("d'Alembert", document.Groups[0].Channels[0].Name);
        Assert.Equal(2, document.Groups[0].Channels[0].SampleCount);
    }

    [Fact]
    public void ReadingWithAChannelFilterDecodesOnlyTheSelectedChannels()
    {
        var options = new TdmsReadOptions
        {
            ChannelFilter = new HashSet<string>([SampleFiles.Counter], StringComparer.Ordinal),
        };
        var document = Parse(SampleFiles.BuildKitchenSink().Build(), options);

        Assert.NotNull(document.FindChannelByPath(SampleFiles.Counter)!.Data);
        Assert.Null(document.FindChannelByPath(SampleFiles.Voltage)!.Data);

        // Skipped channels are still counted correctly.
        Assert.Equal(16, document.FindChannelByPath(SampleFiles.Ticks)!.SampleCount);
        Assert.Equal(19, document.FindChannelByPath(SampleFiles.Voltage)!.SampleCount);
    }

    [Fact]
    public void TheGoldenVectorOfTheWebEngineParsesIdentically()
    {
        // packages/engine/vectors/tdms.json in testbench-tools: group1, 100 samples,
        // 2 segments, ch1 = 0 step 0.5, ch2 = 1000 step -1, unit V, gain 1.5, title roundtrip.
        var ch1 = Enumerable.Range(0, 100).Select(i => (object)(i * 0.5)).ToList();
        var ch2 = Enumerable.Range(0, 100).Select(i => (object)(1000.0 - i)).ToList();

        var writer = new TdmsTestWriter();
        var first = writer.AddSegment();
        first.AddObject(TdmsPath.Root, ("title", TdmsPropertyValue.FromString("roundtrip")));
        first.AddChannel(
            TdmsPath.ForChannel("group1", "ch1"),
            TdmsDataType.F64,
            ch1.Take(50).ToList(),
            ("unit", TdmsPropertyValue.FromString("V")),
            ("gain", TdmsPropertyValue.FromDouble(1.5)));
        first.AddChannel(
            TdmsPath.ForChannel("group1", "ch2"),
            TdmsDataType.F64,
            ch2.Take(50).ToList(),
            ("unit", TdmsPropertyValue.FromString("V")),
            ("gain", TdmsPropertyValue.FromDouble(1.5)));

        var second = writer.AddSegment(newObjectList: false);
        second.AddIncrementalChannel(TdmsPath.ForChannel("group1", "ch1"), TdmsDataType.F64, ch1.Skip(50).ToList());
        second.AddIncrementalChannel(TdmsPath.ForChannel("group1", "ch2"), TdmsDataType.F64, ch2.Skip(50).ToList());

        var document = Parse(writer.Build());

        Assert.Equal("roundtrip", document.Properties["title"].ToInvariantString());
        var parsed1 = document.FindChannel("group1", "ch1")!;
        var parsed2 = document.FindChannel("group1", "ch2")!;
        Assert.Equal(100, parsed1.SampleCount);
        Assert.Equal(100, parsed2.SampleCount);
        Assert.Equal("V", parsed1.Properties["unit"].ToInvariantString());
        Assert.Equal("1.5", parsed2.Properties["gain"].ToInvariantString());
        for (var i = 0; i < 100; i++)
        {
            Assert.True(parsed1.Data!.TryGetDouble(i, out var v1));
            Assert.True(parsed2.Data!.TryGetDouble(i, out var v2));
            Assert.Equal(i * 0.5, v1);
            Assert.Equal(1000.0 - i, v2);
        }
    }
}
