namespace Tdms.Core.Tests;

/// <summary>
/// The result must depend only on the bytes, never on how they were sliced — the parser is
/// fed from a UI thread that hands it whatever a read returned.
/// </summary>
public sealed class StreamingTests
{
    private static string ParseInChunks(byte[] bytes, int chunkSize)
    {
        var parser = new TdmsStreamParser();
        for (var offset = 0; offset < bytes.Length; offset += chunkSize)
        {
            parser.Push(bytes.AsSpan(offset, Math.Min(chunkSize, bytes.Length - offset)));
        }

        return TestFormat.Dump(parser.Finish());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(13)]
    [InlineData(64)]
    [InlineData(255)]
    [InlineData(4096)]
    public void ChunkSizeDoesNotChangeTheResult(int chunkSize)
    {
        var bytes = SampleFiles.BuildKitchenSink().Build();
        var whole = ParseInChunks(bytes, bytes.Length);

        Assert.Equal(whole, ParseInChunks(bytes, chunkSize));
    }

    [Fact]
    public void ByteAtATimeMatchesAWholeFilePush()
    {
        var bytes = SampleFiles.BuildKitchenSink().Build();

        var oneByte = ParseInChunks(bytes, 1);
        var whole = ParseInChunks(bytes, bytes.Length);

        Assert.Equal(whole, oneByte);
        Assert.Contains("channel\tVoltage", whole, StringComparison.Ordinal);
    }

    [Fact]
    public void IrregularChunkSizesMatchTooEvenAcrossSegmentBoundaries()
    {
        var bytes = SampleFiles.BuildKitchenSink().Build();
        var expected = ParseInChunks(bytes, bytes.Length);

        var parser = new TdmsStreamParser();
        int[] pattern = [1, 3, 7, 13, 4096, 2, 5000, 11];
        var offset = 0;
        var index = 0;
        while (offset < bytes.Length)
        {
            var size = Math.Min(pattern[index++ % pattern.Length], bytes.Length - offset);
            parser.Push(bytes.AsSpan(offset, size));
            offset += size;
        }

        Assert.Equal(expected, TestFormat.Dump(parser.Finish()));
    }

    [Fact]
    public void StreamingToASinkNeverStoresTheSamples()
    {
        var bytes = SampleFiles.BuildKitchenSink().Build();
        var sink = new CountingSink();
        var options = new TdmsReadOptions { StoreValues = false, Sink = sink };

        var document = TdmsFileReader.Read(new MemoryStream(bytes), options);

        Assert.All(document.Channels, c => Assert.Null(c.Data));
        Assert.Equal(document.TotalSampleCount, sink.Samples);
        Assert.True(sink.Finished);
        Assert.True(sink.Chunks >= 4);
    }

    [Fact]
    public void PushAfterFinishIsRejected()
    {
        var parser = new TdmsStreamParser();
        parser.Push(SampleFiles.BuildKitchenSink().Build());
        parser.Finish();

        Assert.Throws<InvalidOperationException>(() => parser.Push(new byte[4]));
    }

    private sealed class CountingSink : TdmsDataSink
    {
        public long Samples { get; private set; }

        public int Chunks { get; private set; }

        public bool Finished { get; private set; }

        public override void OnSamples(TdmsChannelRef channel, TdmsSampleBuffer samples) =>
            Samples += samples.Count;

        public override void OnChunkCompleted() => Chunks++;

        public override void OnFinished() => Finished = true;
    }
}
