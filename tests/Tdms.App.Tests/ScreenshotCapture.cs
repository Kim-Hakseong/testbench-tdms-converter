using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Tdms.App.ViewModels;
using Tdms.App.Views;
using Xunit;

namespace Tdms.App.Tests;

/// <summary>
/// Not a behaviour test: this renders the README screenshot from a real demo measurement, so
/// the image in the repository always matches the current UI.
/// </summary>
public sealed class ScreenshotCapture : IDisposable
{
    private const string OutputPath = "/tmp/tdms-converter-screenshot.png";

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "tdms-shot-" + Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task CaptureMainWindowScreenshot()
    {
        var path = DemoFile.Write(_directory);

        var viewModel = new MainWindowViewModel();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        await viewModel.OpenAsync(path);
        Assert.True(viewModel.HasDocument);

        // Show a channel with a full property set rather than the file node.
        viewModel.SelectedNode = viewModel.Nodes[0].Children[0].Children[0];
        Assert.Equal("TC1", viewModel.SelectedTitle);
        Assert.NotEmpty(viewModel.SelectedProperties);

        // Grid cells and tree items only materialise on the next render pass.
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(120);
            Dispatcher.UIThread.RunJobs();
        }

        var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);
        using (var stream = File.Create(OutputPath))
        {
            bitmap!.Save(stream);
        }

        Assert.True(new FileInfo(OutputPath).Length > 10_000);
        window.Close();
    }
}
