using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Tdms.App.Localization;
using Tdms.Core;
using Tdms.Core.Export;
using Tdms.Core.Model;

namespace Tdms.App.ViewModels;

/// <summary>One selectable channel in the export dialog.</summary>
public sealed partial class ExportChannelViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected = true;

    /// <summary>Creates the entry from a channel.</summary>
    /// <param name="channel">Channel to offer.</param>
    public ExportChannelViewModel(TdmsChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        Path = channel.Path;
        DisplayName = $"{channel.GroupName} / {channel.Name}";
        Detail = string.Format(
            CultureInfo.CurrentCulture,
            "{0} · {1} {2}",
            TdmsDataTypes.Name(channel.DataType),
            channel.SampleCount.ToString("N0", CultureInfo.CurrentCulture),
            Loc.T("UnitSamples"));
    }

    /// <summary>TDMS object path.</summary>
    public string Path { get; }

    /// <summary>Group and channel name.</summary>
    public string DisplayName { get; }

    /// <summary>Data type and sample count.</summary>
    public string Detail { get; }
}

/// <summary>State of the export dialog: what to write, where, and how far it got.</summary>
public sealed partial class ExportViewModel : ObservableObject
{
    private readonly TdmsDocument _document;
    private CancellationTokenSource? _cancellation;

    [ObservableProperty]
    private int _formatIndex;

    [ObservableProperty]
    private int _delimiterIndex;

    [ObservableProperty]
    private bool _includeTimeColumn = true;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isError;

    /// <summary>Creates the dialog state for a document.</summary>
    /// <param name="document">The open document.</param>
    public ExportViewModel(TdmsDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _document = document;
        Channels = new ObservableCollection<ExportChannelViewModel>(
            document.Channels.Select(c => new ExportChannelViewModel(c)));
    }

    /// <summary>Every channel of the file, each with a checkbox.</summary>
    public ObservableCollection<ExportChannelViewModel> Channels { get; }

    /// <summary>Format names for the combo box, in <see cref="TdmsExporters.All"/> order.</summary>
    public static string[] FormatOptions =>
        TdmsExporters.All.Select(e => Loc.T(e.DisplayNameKey)).ToArray();

    /// <summary>Delimiter names for the combo box.</summary>
    public static string[] DelimiterOptions =>
        [Loc.T("DelimiterComma"), Loc.T("DelimiterSemicolon"), Loc.T("DelimiterTab")];

    /// <summary>Field separator matching <see cref="DelimiterIndex"/>.</summary>
    public char Delimiter => DelimiterIndex switch
    {
        1 => ';',
        2 => '\t',
        _ => ',',
    };

    /// <summary>
    /// Whether the delimiter selector applies. A workbook has cells, not separated text,
    /// so the choice is meaningless for xlsx and the control greys out rather than
    /// implying it does something.
    /// </summary>
    public bool DelimiterApplies => Exporter.FileExtension != ".xlsx";

    /// <summary>Exporter matching <see cref="FormatIndex"/>.</summary>
    public ITdmsExporter Exporter =>
        TdmsExporters.All[Math.Clamp(FormatIndex, 0, TdmsExporters.All.Count - 1)];

    /// <summary>Object paths of the checked channels, in tree order.</summary>
    public IReadOnlyList<string> SelectedPaths =>
        Channels.Where(c => c.IsSelected).Select(c => c.Path).ToList();

    /// <summary>Suggested file name for the save dialog.</summary>
    public string SuggestedFileName
    {
        get
        {
            var stem = _document.SourcePath is { } path
                ? System.IO.Path.GetFileNameWithoutExtension(path)
                : "export";
            return stem + Exporter.FileExtension;
        }
    }

    /// <summary>Checks every channel.</summary>
    public void SelectAll() => SetAll(true);

    /// <summary>Unchecks every channel.</summary>
    public void SelectNone() => SetAll(false);

    /// <summary>Stops a running export.</summary>
    public void Cancel() => _cancellation?.Cancel();

    /// <summary>
    /// Streams the selected channels into a file. The source is read chunk by chunk, so the
    /// memory used does not grow with the size of the measurement.
    /// </summary>
    /// <param name="targetPath">Destination file.</param>
    /// <returns><see langword="true"/> when the file was written.</returns>
    public async Task<bool> ExportAsync(string targetPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetPath);

        var paths = SelectedPaths;
        if (paths.Count == 0)
        {
            IsError = true;
            Message = Loc.T("NoChannelsSelected");
            return false;
        }

        if (_document.SourcePath is not { } source)
        {
            IsError = true;
            Message = Loc.T("ExportFailed");
            return false;
        }

        IsRunning = true;
        IsError = false;
        ProgressPercent = 0;
        Message = Loc.T("Exporting");

        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        var progress = new Progress<TdmsExportProgress>(p => ProgressPercent = p.Fraction * 100);

        var request = new TdmsExportRequest
        {
            SourcePath = source,
            ChannelPaths = paths,
            Metadata = _document,
            Delimiter = Delimiter,
            IncludeWaveformTimeColumn = IncludeTimeColumn,
            Progress = progress,
        };
        var exporter = Exporter;

        try
        {
            await Task.Run(
                () =>
                {
                    using var output = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    exporter.Export(request, output, cancellation.Token);
                },
                cancellation.Token).ConfigureAwait(true);

            ProgressPercent = 100;
            Message = $"{Loc.T("ExportDone")} · {System.IO.Path.GetFileName(targetPath)}";
            return true;
        }
        catch (OperationCanceledException)
        {
            Message = Loc.T("ExportCancelled");
            TryDelete(targetPath);
            return false;
        }
        catch (Exception exception) when (exception is TdmsException or IOException or UnauthorizedAccessException)
        {
            IsError = true;
            Message = $"{Loc.T("ExportFailed")}: {exception.Message}";
            TryDelete(targetPath);
            return false;
        }
        finally
        {
            _cancellation = null;
            IsRunning = false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // A half written file we cannot remove is not worth failing over.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void SetAll(bool selected)
    {
        foreach (var channel in Channels)
        {
            channel.IsSelected = selected;
        }
    }

    partial void OnFormatIndexChanged(int value)
    {
        OnPropertyChanged(nameof(SuggestedFileName));
        OnPropertyChanged(nameof(DelimiterApplies));
    }
}
