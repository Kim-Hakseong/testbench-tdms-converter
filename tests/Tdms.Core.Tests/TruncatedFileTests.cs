using Tdms.Core.Model;

namespace Tdms.Core.Tests;

/// <summary>
/// Measurement files are routinely killed mid-write. Everything before the incomplete tail
/// must survive; only the values that were never fully written are dropped.
/// </summary>
public sealed class TruncatedFileTests
{
    private static string ChannelA => TdmsPath.ForChannel("Run", "A");

    private static string ChannelB => TdmsPath.ForChannel("Run", "B");

    /// <summary>Two f64 channels, two values each per chunk, then a clipped final segment.</summary>
    private static TdmsTestWriter BuildInterruptedFile(int truncatedRawBytes)
    {
        var writer = new TdmsTestWriter();

        var first = writer.AddSegment();
        first.AddObject(TdmsPath.Root, ("title", TdmsPropertyValue.FromString("interrupted")));
        first.AddChannel(ChannelA, TdmsDataType.F64, [1.0, 2.0], ("unit_string", TdmsPropertyValue.FromString("V")));
        first.AddChannel(ChannelB, TdmsDataType.F64, [10.0, 20.0]);

        var second = writer.AddSegment(newObjectList: false);
        second.AddIncrementalChannel(ChannelA, TdmsDataType.F64, [3.0, 4.0]);
        second.AddIncrementalChannel(ChannelB, TdmsDataType.F64, [30.0, 40.0]);

        var killed = writer.AddSegment(newObjectList: false, includeMetadata: false);
        killed.AddChannel(ChannelA, TdmsDataType.F64, [5.0, 6.0]);
        killed.AddChannel(ChannelB, TdmsDataType.F64, [50.0, 60.0]);
        killed.TruncatedRawBytes = truncatedRawBytes;

        return writer;
    }

    [Fact]
    public void ACompleteFileIsNotFlaggedAsTruncated()
    {
        var writer = BuildInterruptedFile(truncatedRawBytes: 32);
        writer.Segments[^1].TruncatedRawBytes = null;

        var document = TdmsFileReader.Read(new MemoryStream(writer.Build()), new TdmsReadOptions());

        Assert.False(document.IsTruncated);
        Assert.Equal(6, document.FindChannelByPath(ChannelA)!.SampleCount);
    }

    [Fact]
    public void AnIncompleteTailKeepsTheCompleteValuesAndDropsTheRest()
    {
        // 24 of the 32 raw bytes were written: A got both values, B only its first.
        var document = TdmsFileReader.Read(new MemoryStream(BuildInterruptedFile(24).Build()), new TdmsReadOptions());

        Assert.True(document.IsTruncated);

        var a = document.FindChannelByPath(ChannelA)!;
        var b = document.FindChannelByPath(ChannelB)!;
        Assert.Equal(6, a.SampleCount);
        Assert.Equal(5, b.SampleCount);
        AssertValues(a, [1.0, 2.0, 3.0, 4.0, 5.0, 6.0]);
        AssertValues(b, [10.0, 20.0, 30.0, 40.0, 50.0]);

        // The properties written before the interruption are intact.
        Assert.Equal("interrupted", document.Properties["title"].ToInvariantString());
        Assert.Equal("V", a.Properties["unit_string"].ToInvariantString());
    }

    [Fact]
    public void AHalfWrittenValueIsDroppedRatherThanGuessed()
    {
        // 20 bytes: A complete, then 4 bytes of B's 8-byte first value.
        var document = TdmsFileReader.Read(new MemoryStream(BuildInterruptedFile(20).Build()), new TdmsReadOptions());

        Assert.True(document.IsTruncated);
        Assert.Equal(6, document.FindChannelByPath(ChannelA)!.SampleCount);
        Assert.Equal(4, document.FindChannelByPath(ChannelB)!.SampleCount);
    }

    [Fact]
    public void NoRawBytesAtAllStillKeepsTheEarlierSegments()
    {
        var document = TdmsFileReader.Read(new MemoryStream(BuildInterruptedFile(0).Build()), new TdmsReadOptions());

        Assert.True(document.IsTruncated);
        Assert.Equal(4, document.FindChannelByPath(ChannelA)!.SampleCount);
        Assert.Equal(4, document.FindChannelByPath(ChannelB)!.SampleCount);
    }

    [Fact]
    public void AFileCutInsideALeadInIsToleratedByDefault()
    {
        var complete = BuildInterruptedFile(32);
        complete.Segments[^1].TruncatedRawBytes = null;
        var bytes = complete.Build();

        // Ten bytes into what would have been another lead-in.
        var clipped = bytes.Concat("TDSm"u8.ToArray()).Concat(new byte[6]).ToArray();

        var document = TdmsFileReader.Read(new MemoryStream(clipped), new TdmsReadOptions());

        Assert.True(document.IsTruncated);
        Assert.Equal(6, document.FindChannelByPath(ChannelA)!.SampleCount);
    }

    [Fact]
    public void AFileCutInsideALeadInCanBeRejectedInstead()
    {
        var complete = BuildInterruptedFile(32);
        complete.Segments[^1].TruncatedRawBytes = null;
        var clipped = complete.Build().Concat("TDSm"u8.ToArray()).Concat(new byte[6]).ToArray();

        var options = new TdmsReadOptions { TolerateTruncatedTail = false };

        var error = Assert.Throws<TdmsFormatException>(
            () => TdmsFileReader.Read(new MemoryStream(clipped), options));
        Assert.Contains("incomplete", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MetadataOnlyReadsAgreeWithFullReadsOnATruncatedFile()
    {
        var bytes = BuildInterruptedFile(24).Build();

        var full = TdmsFileReader.Read(new MemoryStream(bytes), new TdmsReadOptions());
        var scanned = TdmsFileReader.Read(new MemoryStream(bytes), TdmsReadOptions.MetadataOnly);

        Assert.True(scanned.IsTruncated);
        foreach (var channel in full.Channels)
        {
            Assert.Equal(channel.SampleCount, scanned.FindChannelByPath(channel.Path)!.SampleCount);
        }
    }

    private static void AssertValues(TdmsChannel channel, double[] expected)
    {
        Assert.NotNull(channel.Data);
        Assert.Equal(expected.Length, channel.Data!.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.True(channel.Data.TryGetDouble(i, out var value));
            Assert.Equal(expected[i], value);
        }
    }
}
