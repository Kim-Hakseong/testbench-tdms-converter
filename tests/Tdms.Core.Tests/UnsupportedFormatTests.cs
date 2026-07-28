namespace Tdms.Core.Tests;

/// <summary>
/// What the reader cannot do, it says out loud. A converter that silently produces wrong
/// numbers is worse than one that refuses.
/// </summary>
public sealed class UnsupportedFormatTests
{
    private const uint TocInterleavedData = 1u << 5;
    private const uint TocBigEndian = 1u << 6;
    private const uint TocDaqmxRawData = 1u << 7;

    private static byte[] BuildWithToc(uint extraToc)
    {
        var writer = new TdmsTestWriter { ExtraToc = extraToc };
        var segment = writer.AddSegment();
        segment.AddChannel(TdmsPath.ForChannel("g", "c"), TdmsDataType.F64, [1.0, 2.0]);
        return writer.Build();
    }

    private static void Read(byte[] bytes) =>
        TdmsFileReader.Read(new MemoryStream(bytes), new TdmsReadOptions());

    [Fact]
    public void BigEndianSegmentsAreRejectedByName()
    {
        var error = Assert.Throws<TdmsUnsupportedFeatureException>(() => Read(BuildWithToc(TocBigEndian)));

        Assert.Contains("Big-endian", error.Message, StringComparison.Ordinal);
        Assert.Contains("kTocBigEndian", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InterleavedRawDataIsRejectedByName()
    {
        var error = Assert.Throws<TdmsUnsupportedFeatureException>(() => Read(BuildWithToc(TocInterleavedData)));

        Assert.Contains("Interleaved", error.Message, StringComparison.Ordinal);
        Assert.Contains("kTocInterleavedData", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DaqmxSegmentsAreRejectedByName()
    {
        var error = Assert.Throws<TdmsUnsupportedFeatureException>(() => Read(BuildWithToc(TocDaqmxRawData)));

        Assert.Contains("DAQmx", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADaqmxRawIndexIsRejectedEvenWithoutTheTocBit()
    {
        var writer = new TdmsTestWriter();
        var segment = writer.AddSegment();
        segment.Objects.Add(new TdmsTestWriter.WriterObject
        {
            Path = TdmsPath.ForChannel("g", "c"),
            DaqmxIndex = true,
        });

        var error = Assert.Throws<TdmsUnsupportedFeatureException>(() => Read(writer.Build()));

        Assert.Contains("DAQmx", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SomethingThatIsNotTdmsIsRejected()
    {
        var error = Assert.Throws<TdmsFormatException>(
            () => Read("this is a CSV, not a TDMS file, and it is long enough."u8.ToArray()));

        Assert.Contains("TDSm", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AShortFileThatIsNotTdmsIsRejectedToo()
    {
        // Too short to hold a lead-in: the tag is still checked byte by byte.
        var error = Assert.Throws<TdmsFormatException>(() => Read("a,b,c\n1,2,3\n"u8.ToArray()));

        Assert.Contains("TDSm", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyFileParsesToAnEmptyDocument()
    {
        var document = TdmsFileReader.Read(new MemoryStream([]), new TdmsReadOptions());

        Assert.Empty(document.Groups);
        Assert.False(document.IsTruncated);
        Assert.Equal(0, document.SegmentCount);
    }

    [Fact]
    public void AReusedRawIndexThatWasNeverDeclaredIsRejected()
    {
        var writer = new TdmsTestWriter();
        var segment = writer.AddSegment();
        segment.AddIncrementalChannel(TdmsPath.ForChannel("g", "c"), TdmsDataType.F64, [1.0]);

        var error = Assert.Throws<TdmsFormatException>(() => Read(writer.Build()));

        Assert.Contains("never declared", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AChannelThatChangesItsTypeMidFileIsRejected()
    {
        var writer = new TdmsTestWriter();
        var path = TdmsPath.ForChannel("g", "c");
        writer.AddSegment().AddChannel(path, TdmsDataType.F64, [1.0]);
        writer.AddSegment().AddChannel(path, TdmsDataType.I32, [1]);

        var error = Assert.Throws<TdmsUnsupportedFeatureException>(() => Read(writer.Build()));

        Assert.Contains("changes its data type", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StringChannelsAreSupportedUnlikeTheBrowserEngine()
    {
        var writer = new TdmsTestWriter();
        writer.AddSegment().AddChannel(
            TdmsPath.ForChannel("g", "notes"),
            TdmsDataType.String,
            ["first", string.Empty, "third value"]);

        var document = TdmsFileReader.Read(new MemoryStream(writer.Build()), new TdmsReadOptions());

        var channel = document.FindChannel("g", "notes")!;
        Assert.Equal(3, channel.SampleCount);
        Assert.Equal("first", channel.Data!.GetText(0));
        Assert.Equal(string.Empty, channel.Data.GetText(1));
        Assert.Equal("third value", channel.Data.GetText(2));
    }
}
