using System.Globalization;

namespace Tdms.Core.Tests;

/// <summary>Paths, timestamps, property values and sample buffers.</summary>
public sealed class PrimitiveTests
{
    [Theory]
    [InlineData("/", new string[0])]
    [InlineData("/'Group'", new[] { "Group" })]
    [InlineData("/'Group'/'Channel'", new[] { "Group", "Channel" })]
    [InlineData("/'O''Hara'", new[] { "O'Hara" })]
    [InlineData("/'a''b'/'c''''d'", new[] { "a'b", "c''d" })]
    public void PathsParseIntoTheirComponents(string path, string[] expected) =>
        Assert.Equal(expected, TdmsPath.Parse(path));

    [Theory]
    [InlineData("Group")]
    [InlineData("O'Hara")]
    [InlineData("quote'''storm")]
    public void PathsRoundTripThroughEscaping(string name)
    {
        Assert.Equal([name], TdmsPath.Parse(TdmsPath.ForGroup(name)));
        Assert.Equal([name, name], TdmsPath.Parse(TdmsPath.ForChannel(name, name)));
    }

    [Theory]
    [InlineData("Group")]
    [InlineData("/Group")]
    [InlineData("/'unterminated")]
    public void MalformedPathsAreRejected(string path) =>
        Assert.Throws<TdmsFormatException>(() => TdmsPath.Parse(path));

    [Fact]
    public void TheTimestampEpochIsNineteenOhFour()
    {
        Assert.Equal(
            new DateTimeOffset(1904, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new TdmsTimestamp(0, 0).ToDateTimeOffset());
        Assert.Equal("1904-01-01T00:00:00.0000000Z", new TdmsTimestamp(0, 0).ToString());
    }

    [Fact]
    public void TimestampsConvertBothWays()
    {
        var instant = new DateTimeOffset(2026, 7, 28, 13, 45, 30, 250, TimeSpan.Zero);

        var timestamp = TdmsTimestamp.FromDateTimeOffset(instant);

        Assert.Equal(instant, timestamp.ToDateTimeOffset());
        Assert.Equal(0.25, timestamp.FractionalSeconds, 9);
    }

    [Fact]
    public void TimestampsBeforeTheEpochUseNegativeSeconds()
    {
        var instant = new DateTimeOffset(1900, 5, 4, 0, 0, 0, TimeSpan.Zero);

        var timestamp = TdmsTimestamp.FromDateTimeOffset(instant);

        Assert.True(timestamp.Seconds < 0);
        Assert.Equal(instant, timestamp.ToDateTimeOffset());
    }

    [Fact]
    public void TimestampsCompareChronologically()
    {
        var early = new TdmsTimestamp(100, 0);
        var late = new TdmsTimestamp(100, 5_000_000_000_000_000_000);

        Assert.True(early < late);
        Assert.True(late > early);
        Assert.True(early <= new TdmsTimestamp(100, 0));
        Assert.True(early >= new TdmsTimestamp(100, 0));
        Assert.Equal(early, new TdmsTimestamp(100, 0));
        Assert.NotEqual(early, late);
        Assert.Equal(early.GetHashCode(), new TdmsTimestamp(100, 0).GetHashCode());
    }

    [Fact]
    public void UnixSecondsUseTheUsualEpoch()
    {
        var timestamp = TdmsTimestamp.FromDateTimeOffset(DateTimeOffset.UnixEpoch);

        Assert.Equal(0, timestamp.ToUnixSeconds(), 6);
        Assert.Equal(2082844800L, timestamp.Seconds);
    }

    [Fact]
    public void PropertyValuesFormatWithoutTheCurrentCulture()
    {
        Assert.Equal("1.5", TdmsPropertyValue.FromDouble(1.5).ToInvariantString());
        Assert.Equal("true", TdmsPropertyValue.FromBoolean(true).ToInvariantString());
        Assert.Equal("42", TdmsPropertyValue.FromInt32(42).ToInvariantString());
        Assert.Equal("text", TdmsPropertyValue.FromString("text").ToInvariantString());
        Assert.Equal(string.Empty, default(TdmsPropertyValue).ToInvariantString());
    }

    [Fact]
    public void PropertyValuesExposeNumbersAndTimestamps()
    {
        Assert.True(TdmsPropertyValue.FromDouble(2.5).TryGetDouble(out var number));
        Assert.Equal(2.5, number);
        Assert.True(TdmsPropertyValue.FromInt32(3).TryGetDouble(out var integer));
        Assert.Equal(3, integer);
        Assert.False(TdmsPropertyValue.FromString("x").TryGetDouble(out _));

        var stamp = TdmsTimestamp.FromDateTimeOffset(DateTimeOffset.UnixEpoch);
        Assert.True(TdmsPropertyValue.FromTimestamp(stamp).TryGetTimestamp(out var read));
        Assert.Equal(stamp, read);
        Assert.False(TdmsPropertyValue.FromDouble(1).TryGetTimestamp(out _));
    }

    [Fact]
    public void PropertyValuesCompareByTypeAndValue()
    {
        Assert.Equal(TdmsPropertyValue.FromString("a"), TdmsPropertyValue.FromString("a"));
        Assert.True(TdmsPropertyValue.FromDouble(1) == TdmsPropertyValue.FromDouble(1));
        Assert.True(TdmsPropertyValue.FromDouble(1) != TdmsPropertyValue.FromInt32(1));
    }

    [Fact]
    public void DataTypeNamesAreTheOnesUsedInTheUi()
    {
        Assert.Equal("f64", TdmsDataTypes.Name(TdmsDataType.F64));
        Assert.Equal("timestamp", TdmsDataTypes.Name(TdmsDataType.Timestamp));
        Assert.Equal("dtype(77)", TdmsDataTypes.Name((TdmsDataType)77));
    }

    [Theory]
    [InlineData(TdmsDataType.I8, 1)]
    [InlineData(TdmsDataType.U16, 2)]
    [InlineData(TdmsDataType.F32, 4)]
    [InlineData(TdmsDataType.F64, 8)]
    [InlineData(TdmsDataType.Boolean, 1)]
    [InlineData(TdmsDataType.Timestamp, 16)]
    public void FixedWidthTypesReportTheirSize(TdmsDataType type, int expected)
    {
        Assert.True(TdmsDataTypes.TryGetFixedSize(type, out var size));
        Assert.Equal(expected, size);
    }

    [Fact]
    public void StringsHaveNoFixedSize() =>
        Assert.False(TdmsDataTypes.TryGetFixedSize(TdmsDataType.String, out _));

    [Fact]
    public void PropertyMapsKeepTheirInsertionOrder()
    {
        var map = new OrderedPropertyMap();
        map.Set("z", TdmsPropertyValue.FromInt32(1));
        map.Set("a", TdmsPropertyValue.FromInt32(2));
        map.Set("z", TdmsPropertyValue.FromInt32(3));

        Assert.Equal(["z", "a"], map.Keys);
        Assert.Equal(2, map.Count);
        Assert.Equal("3", map["z"].ToInvariantString());
        Assert.True(map.ContainsKey("a"));
        Assert.False(map.ContainsKey("q"));
        Assert.Equal(["z", "a"], map.Select(kv => kv.Key));
    }

    [Fact]
    public void SampleBuffersReportTheirTypeFaithfully()
    {
        var buffer = new TdmsSampleBuffer(TdmsDataType.F32);
        var other = new TdmsSampleBuffer(TdmsDataType.F64);

        Assert.Equal(0, buffer.Count);
        Assert.Throws<ArgumentException>(() => buffer.Append(other));
        Assert.Throws<InvalidOperationException>(() => buffer.GetTimestamp(0));
    }

    [Fact]
    public void AWaveformTimeAxisIsDerivedNotStored()
    {
        var writer = new TdmsTestWriter();
        writer.AddSegment().AddChannel(
            TdmsPath.ForChannel("g", "wave"),
            TdmsDataType.F64,
            [1.0, 2.0, 3.0],
            ("wf_increment", TdmsPropertyValue.FromDouble(0.5)),
            ("wf_start_offset", TdmsPropertyValue.FromDouble(10)),
            ("wf_start_time", TdmsPropertyValue.FromTimestamp(new TdmsTimestamp(3_000_000_000, 0))));

        var channel = TdmsFileReader
            .Read(new MemoryStream(writer.Build()), new TdmsReadOptions())
            .FindChannel("g", "wave")!;

        Assert.True(channel.HasWaveformTiming);
        Assert.Equal(10, channel.WaveformStartOffset);
        Assert.True(channel.TryGetRelativeTime(4, out var seconds));
        Assert.Equal(12, seconds, 10);
        Assert.True(channel.TryGetAbsoluteTime(4, out var absolute));
        Assert.Equal(
            new TdmsTimestamp(3_000_000_000, 0).ToDateTimeOffset().AddSeconds(12),
            absolute);

        // Only three samples are stored; the time axis costs nothing.
        Assert.Equal(3, channel.Data!.Count);
    }

    [Fact]
    public void AChannelWithoutTimingSaysSo()
    {
        var writer = new TdmsTestWriter();
        writer.AddSegment().AddChannel(TdmsPath.ForChannel("g", "plain"), TdmsDataType.F64, [1.0]);

        var channel = TdmsFileReader
            .Read(new MemoryStream(writer.Build()), new TdmsReadOptions())
            .FindChannel("g", "plain")!;

        Assert.False(channel.HasWaveformTiming);
        Assert.Null(channel.WaveformIncrement);
        Assert.Null(channel.WaveformStartTime);
        Assert.False(channel.TryGetRelativeTime(1, out _));
        Assert.False(channel.TryGetAbsoluteTime(1, out _));
    }

    [Fact]
    public void GroupAndDocumentLookupsAreOrdinal()
    {
        var document = TdmsFileReader.Read(
            new MemoryStream(SampleFiles.BuildKitchenSink().Build()),
            TdmsReadOptions.MetadataOnly);

        Assert.NotNull(document.FindGroup(SampleFiles.AnalogGroup));
        Assert.Null(document.FindGroup(SampleFiles.AnalogGroup.ToUpper(CultureInfo.InvariantCulture)));
        Assert.NotNull(document.FindChannel(SampleFiles.AnalogGroup, "Voltage"));
        Assert.Null(document.FindChannel(SampleFiles.AnalogGroup, "voltage"));
        Assert.Null(document.FindChannelByPath("/'nope'/'nope'"));
        Assert.Equal(document.TotalSampleCount, document.Groups.Sum(g => g.TotalSampleCount));
    }
}
