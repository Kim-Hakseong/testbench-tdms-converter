using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Tdms.App.ViewModels;
using Tdms.App.Views;
using Tdms.Core.Tests;
using Xunit;

namespace Tdms.App.Tests;

/// <summary>Opening a file has to fill the tree, the stat strip and the property panel.</summary>
public sealed class MainWindowSmokeTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "tdms-app-" + Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public void TheWindowStartsEmptyButUsable()
    {
        var viewModel = new MainWindowViewModel();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        Assert.False(viewModel.HasDocument);
        Assert.Empty(viewModel.Nodes);
        Assert.Equal("—", viewModel.ChannelCountText);
        Assert.False(window.FindControl<Button>("ExportButton")!.IsEnabled);
        Assert.False(window.FindControl<Button>("ReloadButton")!.IsEnabled);
        Assert.True(window.FindControl<Button>("OpenButton")!.IsEnabled);

        window.Close();
    }

    [AvaloniaFact]
    public async Task OpeningAFileFillsTheTreeAndTheStatStrip()
    {
        var path = DemoFile.Write(_directory);
        var viewModel = new MainWindowViewModel();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        await viewModel.OpenAsync(path);

        Assert.True(viewModel.HasDocument);
        Assert.Equal("endurance-run-42.tdms", viewModel.FileNameText);

        var root = Assert.Single(viewModel.Nodes);
        Assert.Equal(TdmsNodeKind.File, root.Kind);
        Assert.Equal(["Thermal", "Vibration", "Events"], root.Children.Select(c => c.Name));
        Assert.Equal(
            ["TC1", "TC2", "TC3", "TC4"],
            root.Children[0].Children.Select(c => c.Name));

        Assert.Equal("3", viewModel.GroupCountText);
        Assert.Equal("9", viewModel.ChannelCountText);
        Assert.Equal(
            MainWindowViewModel.FormatCount((7 * 2 * DemoFile.SamplesPerSegment) + (2 * DemoFile.MarkerCount)),
            viewModel.SampleCountText);
        Assert.NotEqual("—", viewModel.FileSizeText);

        Assert.True(window.FindControl<Button>("ExportButton")!.IsEnabled);
        window.Close();
    }

    [AvaloniaFact]
    public async Task TheIndexSidecarIsPreferredAndReported()
    {
        var path = DemoFile.Write(_directory);
        var viewModel = new MainWindowViewModel();

        await viewModel.OpenAsync(path);

        Assert.True(viewModel.Document!.ReadFromIndexFile);
        Assert.Equal(Tdms.App.Localization.Loc.T("StatusIndex"), viewModel.SourceText);
        Assert.Equal(Tdms.App.Localization.Loc.T("StatusReady"), viewModel.StatusText);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
    }

    [AvaloniaFact]
    public async Task SelectingANodeShowsItsProperties()
    {
        var path = DemoFile.Write(_directory);
        var viewModel = new MainWindowViewModel();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        await viewModel.OpenAsync(path);

        // The file node is selected by default.
        Assert.Contains(viewModel.SelectedProperties, p => p.Name == "name" && p.Value == "Endurance run 42");
        Assert.Contains(viewModel.SelectedProperties, p => p.Name == "datetime" && p.Type == "timestamp");

        var thermocouple = viewModel.Nodes[0].Children[0].Children[0];
        viewModel.SelectedNode = thermocouple;

        Assert.Equal("TC1", viewModel.SelectedTitle);
        Assert.Contains(viewModel.SelectedProperties, p => p.Name == "unit_string" && p.Value == "degC");
        Assert.Contains(viewModel.SelectedProperties, p => p.Name == "wf_increment" && p.Value == "0.1");
        Assert.Contains("f64", thermocouple.Detail, StringComparison.Ordinal);

        window.Close();
    }

    [AvaloniaFact]
    public async Task AFileThatIsNotTdmsReportsAReadableError()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "not-really.tdms");
        await File.WriteAllTextAsync(path, "group,channel,value\n1,2,3\n");

        var viewModel = new MainWindowViewModel();
        await viewModel.OpenAsync(path);

        Assert.False(viewModel.HasDocument);
        Assert.Contains("TDSm", viewModel.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(viewModel.Nodes);
    }

    [AvaloniaFact]
    public async Task ReloadingRebuildsTheTree()
    {
        var path = DemoFile.Write(_directory);
        var viewModel = new MainWindowViewModel();

        await viewModel.OpenAsync(path);
        var before = viewModel.SampleCountText;
        await viewModel.ReloadAsync();

        Assert.Equal(before, viewModel.SampleCountText);
        Assert.True(viewModel.HasDocument);

        viewModel.Clear();
        Assert.False(viewModel.HasDocument);
        await viewModel.ReloadAsync();
        Assert.False(viewModel.HasDocument);
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(2048, "2 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(5L * 1024 * 1024 * 1024, "5 GB")]
    public void FileSizesAreFormattedForTheStatTile(long bytes, string expected) =>
        Assert.Equal(expected, MainWindowViewModel.FormatSize(bytes));

    [AvaloniaFact]
    public async Task ATruncatedFileIsOpenedAndFlagged()
    {
        Directory.CreateDirectory(_directory);
        var writer = new TdmsTestWriter();
        var path = Tdms.Core.TdmsPath.ForChannel("Run", "A");
        writer.AddSegment().AddChannel(path, Tdms.Core.TdmsDataType.F64, [1.0, 2.0]);
        var killed = writer.AddSegment(newObjectList: false, includeMetadata: false);
        killed.AddChannel(path, Tdms.Core.TdmsDataType.F64, [3.0, 4.0]);
        killed.TruncatedRawBytes = 8;
        var file = writer.WriteTo(_directory, "killed");

        var viewModel = new MainWindowViewModel();
        await viewModel.OpenAsync(file);

        Assert.True(viewModel.HasDocument);
        Assert.True(viewModel.Document!.IsTruncated);
        Assert.Equal(Tdms.App.Localization.Loc.T("StatusTruncated"), viewModel.StatusText);
        Assert.Equal("3", viewModel.SampleCountText);
    }
}
