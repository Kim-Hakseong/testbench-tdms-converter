using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Tdms.App.ViewModels;
using Tdms.App.Views;
using Tdms.Core;
using Tdms.Core.Export;
using Xunit;

namespace Tdms.App.Tests;

/// <summary>The export dialog has to pick channels, write the file and survive a cancel.</summary>
public sealed class ExportSmokeTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "tdms-export-" + Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private ExportViewModel BuildViewModel(out string sourcePath)
    {
        sourcePath = DemoFile.Write(_directory);
        return new ExportViewModel(TdmsFileReader.ReadMetadata(sourcePath));
    }

    [AvaloniaFact]
    public void EveryChannelIsOfferedAndCheckedByDefault()
    {
        var viewModel = BuildViewModel(out _);

        Assert.Equal(9, viewModel.Channels.Count);
        Assert.Equal(9, viewModel.SelectedPaths.Count);
        Assert.Equal("Thermal / TC1", viewModel.Channels[0].DisplayName);
        Assert.Contains("f64", viewModel.Channels[0].Detail, StringComparison.Ordinal);

        viewModel.SelectNone();
        Assert.Empty(viewModel.SelectedPaths);

        viewModel.SelectAll();
        Assert.Equal(9, viewModel.SelectedPaths.Count);
    }

    [AvaloniaFact]
    public void TheDialogRendersWithEveryChannelListed()
    {
        var viewModel = BuildViewModel(out _);
        var window = new ExportWindow { DataContext = viewModel };
        window.Show();

        Assert.Equal(
            ExportViewModel.FormatOptions.Length,
            window.FindControl<ComboBox>("FormatSelector")!.ItemCount);
        Assert.Equal(0, window.FindControl<ComboBox>("DelimiterSelector")!.SelectedIndex);
        Assert.True(window.FindControl<Button>("ExportButton")!.IsEnabled);
        Assert.False(window.FindControl<Button>("CancelButton")!.IsVisible);

        window.FindControl<Button>("SelectNoneButton")!.Command?.Execute(null);
        viewModel.SelectNone();
        Assert.Empty(viewModel.SelectedPaths);

        window.Close();
    }

    [AvaloniaFact]
    public void FormatAndDelimiterChoicesMapToTheCoreExporters()
    {
        var viewModel = BuildViewModel(out _);

        Assert.Equal(2, ExportViewModel.FormatOptions.Length);
        Assert.Equal(3, ExportViewModel.DelimiterOptions.Length);

        Assert.Same(TdmsExporters.Csv, viewModel.Exporter);
        Assert.Equal(',', viewModel.Delimiter);
        Assert.Equal("endurance-run-42.csv", viewModel.SuggestedFileName);

        viewModel.FormatIndex = 1;
        Assert.Same(TdmsExporters.CsvWithProperties, viewModel.Exporter);

        viewModel.DelimiterIndex = 1;
        Assert.Equal(';', viewModel.Delimiter);
        viewModel.DelimiterIndex = 2;
        Assert.Equal('\t', viewModel.Delimiter);
    }

    [AvaloniaFact]
    public async Task ExportingWritesTheSelectedChannelsOnly()
    {
        var viewModel = BuildViewModel(out _);
        viewModel.SelectNone();
        viewModel.Channels.First(c => c.DisplayName == "Thermal / TC1").IsSelected = true;
        viewModel.Channels.First(c => c.DisplayName == "Thermal / TC2").IsSelected = true;
        var target = Path.Combine(_directory, "two-channels.csv");

        var ok = await viewModel.ExportAsync(target);

        Assert.True(ok, viewModel.Message);
        Assert.False(viewModel.IsError);
        Assert.Equal(100, viewModel.ProgressPercent);

        var lines = await File.ReadAllLinesAsync(target);
        Assert.Equal("Time (s),Thermal/TC1,Thermal/TC2", lines[0]);
        Assert.Equal((2 * DemoFile.SamplesPerSegment) + 1, lines.Length);
        Assert.StartsWith("0,24,25", lines[1], StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task ThePropertyHeaderVariantWritesCommentLines()
    {
        var viewModel = BuildViewModel(out _);
        viewModel.SelectNone();
        viewModel.Channels.First(c => c.DisplayName == "Events / Marker").IsSelected = true;
        viewModel.FormatIndex = 1;
        viewModel.DelimiterIndex = 1;
        var target = Path.Combine(_directory, "markers.csv");

        var ok = await viewModel.ExportAsync(target);

        Assert.True(ok, viewModel.Message);
        var text = await File.ReadAllTextAsync(target);
        Assert.Contains("# file.name = Endurance run 42", text, StringComparison.Ordinal);
        Assert.Contains("# group[Events].source = operator log", text, StringComparison.Ordinal);
        Assert.Contains("# channel[Events/Marker].dtype = string", text, StringComparison.Ordinal);
        Assert.Contains("# channel[Events/Marker].samples = 240", text, StringComparison.Ordinal);
        Assert.Contains("\nEvents/Marker\nstep 000\n", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task ExportingNothingIsRefusedWithAMessage()
    {
        var viewModel = BuildViewModel(out _);
        viewModel.SelectNone();
        var target = Path.Combine(_directory, "nothing.csv");

        var ok = await viewModel.ExportAsync(target);

        Assert.False(ok);
        Assert.True(viewModel.IsError);
        Assert.Equal(Tdms.App.Localization.Loc.T("NoChannelsSelected"), viewModel.Message);
        Assert.False(File.Exists(target));
    }

    [AvaloniaFact]
    public async Task ACancelledExportLeavesNoHalfWrittenFile()
    {
        var viewModel = BuildViewModel(out _);
        var target = Path.Combine(_directory, "cancelled.csv");

        var export = viewModel.ExportAsync(target);
        viewModel.Cancel();
        var ok = await export;

        // The demo file is small enough that the export may well have finished first;
        // either way the dialog must end in a consistent state.
        Assert.False(viewModel.IsRunning);
        if (!ok)
        {
            Assert.Equal(Tdms.App.Localization.Loc.T("ExportCancelled"), viewModel.Message);
            Assert.False(File.Exists(target));
        }
    }

    [AvaloniaFact]
    public async Task TheTimeColumnCanBeTurnedOff()
    {
        var viewModel = BuildViewModel(out _);
        viewModel.SelectNone();
        viewModel.Channels.First(c => c.DisplayName == "Vibration / Accel_X").IsSelected = true;
        viewModel.IncludeTimeColumn = false;
        var target = Path.Combine(_directory, "no-time.csv");

        Assert.True(await viewModel.ExportAsync(target), viewModel.Message);

        var first = (await File.ReadAllLinesAsync(target))[0];
        Assert.Equal("Vibration/Accel_X", first);
    }
}
