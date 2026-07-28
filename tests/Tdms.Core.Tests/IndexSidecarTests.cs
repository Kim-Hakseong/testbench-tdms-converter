namespace Tdms.Core.Tests;

/// <summary>
/// The <c>.tdms_index</c> sidecar carries the same metadata as the data file with none of the
/// samples, so listing the channels of a 40 GB measurement costs a few hundred kilobytes.
/// </summary>
public sealed class IndexSidecarTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "tdms-tests-" + Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void TheSidecarIsUsedWhenItIsThere()
    {
        var path = SampleFiles.BuildKitchenSink().WriteTo(_directory, "measurement", withIndex: true);

        var document = TdmsFileReader.ReadMetadata(path);

        Assert.True(document.ReadFromIndexFile);
        Assert.Equal(path, document.SourcePath);
        Assert.Equal(new FileInfo(path).Length, document.SourceSizeBytes);
    }

    [Fact]
    public void TheSidecarReportsTheSameTreeCountsAndPropertiesAsTheDataFile()
    {
        var path = SampleFiles.BuildKitchenSink().WriteTo(_directory, "measurement", withIndex: true);

        var fromIndex = TdmsFileReader.ReadMetadata(path);
        var fromData = TdmsFileReader.ReadMetadata(path, useIndexFile: false);

        Assert.True(fromIndex.ReadFromIndexFile);
        Assert.False(fromData.ReadFromIndexFile);
        Assert.Equal(TestFormat.Dump(fromData), TestFormat.Dump(fromIndex));
        Assert.Equal(fromData.TotalSampleCount, fromIndex.TotalSampleCount);
    }

    [Fact]
    public void MetadataOnlyCountsMatchAFullRead()
    {
        var path = SampleFiles.BuildKitchenSink().WriteTo(_directory, "measurement", withIndex: true);

        var full = TdmsFileReader.Read(path);
        var scanned = TdmsFileReader.ReadMetadata(path);

        Assert.Equal(full.ChannelCount, scanned.ChannelCount);
        Assert.Equal(full.TotalSampleCount, scanned.TotalSampleCount);
        foreach (var channel in full.Channels)
        {
            var other = scanned.FindChannelByPath(channel.Path)!;
            Assert.Equal(channel.SampleCount, other.SampleCount);
            Assert.Equal(channel.DataType, other.DataType);
            Assert.Null(other.Data);
        }
    }

    [Fact]
    public void ADamagedSidecarFallsBackToTheDataFile()
    {
        var path = SampleFiles.BuildKitchenSink().WriteTo(_directory, "measurement", withIndex: true);
        File.WriteAllBytes(
            Path.ChangeExtension(path, null) + TdmsFileReader.IndexFileExtension,
            "not an index file at all, but long enough to look like one"u8.ToArray());

        var document = TdmsFileReader.ReadMetadata(path);

        Assert.False(document.ReadFromIndexFile);
        Assert.Equal(8, document.ChannelCount);
    }

    [Fact]
    public void WithoutASidecarTheDataFileIsScanned()
    {
        var path = SampleFiles.BuildKitchenSink().WriteTo(_directory, "measurement");

        Assert.Null(TdmsFileReader.FindIndexFile(path));
        Assert.False(TdmsFileReader.ReadMetadata(path).ReadFromIndexFile);
    }

    [Fact]
    public void AFileLargerThanTheReadBufferIsSkippedOverRatherThanRead()
    {
        // 400k f64 samples over four segments — several megabytes, so the metadata scan has
        // to seek past raw sections instead of reading them.
        const int perSegment = 100_000;
        var values = Enumerable.Range(0, perSegment).Select(i => (object)(i * 0.25)).ToList();
        var writer = new TdmsTestWriter();
        var path = TdmsPath.ForChannel("Big", "Signal");
        writer.AddSegment().AddChannel(path, TdmsDataType.F64, values, ("unit_string", TdmsPropertyValue.FromString("V")));
        for (var i = 0; i < 3; i++)
        {
            writer.AddSegment(newObjectList: false).AddIncrementalChannel(path, TdmsDataType.F64, values);
        }

        var file = writer.WriteTo(_directory, "big");
        Assert.True(new FileInfo(file).Length > 3_000_000);

        var scanned = TdmsFileReader.ReadMetadata(file);

        Assert.Equal(4 * perSegment, scanned.FindChannelByPath(path)!.SampleCount);
        Assert.Equal("V", scanned.FindChannelByPath(path)!.Properties["unit_string"].ToInvariantString());
        Assert.Equal(4, scanned.SegmentCount);
    }
}
