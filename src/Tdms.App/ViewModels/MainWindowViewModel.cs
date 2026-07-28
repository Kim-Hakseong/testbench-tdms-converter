using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Tdms.App.Localization;
using Tdms.Core;
using Tdms.Core.Model;

namespace Tdms.App.ViewModels;

/// <summary>State of the main window: the open file, its tree and the stat strip.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private TdmsNodeViewModel? _selectedNode;

    [ObservableProperty]
    private string _fileNameText = string.Empty;

    [ObservableProperty]
    private string _statusText = Loc.T("StatusNoFile");

    [ObservableProperty]
    private string _sourceText = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _fileSizeText = "—";

    [ObservableProperty]
    private string _groupCountText = "—";

    [ObservableProperty]
    private string _channelCountText = "—";

    [ObservableProperty]
    private string _sampleCountText = "—";

    /// <summary>The document currently open, or <see langword="null"/>.</summary>
    public TdmsDocument? Document { get; private set; }

    /// <summary>Path of the open file, or <see langword="null"/>.</summary>
    public string? FilePath { get; private set; }

    /// <summary>Root nodes of the channel tree.</summary>
    public ObservableCollection<TdmsNodeViewModel> Nodes { get; } = [];

    /// <summary>Whether a file is open and can be exported or reloaded.</summary>
    public bool HasDocument => Document is not null;

    /// <summary>Properties of the selected node.</summary>
    public ObservableCollection<PropertyRowViewModel> SelectedProperties { get; } = [];

    /// <summary>Title line above the property table.</summary>
    public string SelectedTitle => SelectedNode?.Name ?? Loc.T("NoSelection");

    /// <summary>
    /// Reads a file's metadata on a worker thread and rebuilds the tree. Samples are never
    /// loaded here — only the lead-ins, the metadata and the sample counts.
    /// </summary>
    /// <param name="path">Path of the <c>.tdms</c> file.</param>
    /// <returns>A task that completes when the tree is ready.</returns>
    public async Task OpenAsync(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        IsBusy = true;
        ErrorMessage = string.Empty;
        StatusText = Loc.T("StatusLoading");
        FileNameText = Path.GetFileName(path);

        try
        {
            var document = await Task.Run(() => TdmsFileReader.ReadMetadata(path)).ConfigureAwait(true);
            Apply(path, document);
        }
        catch (Exception exception) when (exception is TdmsException or IOException or UnauthorizedAccessException)
        {
            Clear();
            FileNameText = Path.GetFileName(path);
            ErrorMessage = $"{Loc.T("LoadFailed")}: {exception.Message}";
            StatusText = Loc.T("LoadFailed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Re-reads the file that is currently open.</summary>
    /// <returns>A task that completes when the tree is ready.</returns>
    public Task ReloadAsync() => FilePath is { } path ? OpenAsync(path) : Task.CompletedTask;

    /// <summary>Drops the open document and resets the stat strip.</summary>
    public void Clear()
    {
        Document = null;
        FilePath = null;
        Nodes.Clear();
        SelectedNode = null;
        FileNameText = string.Empty;
        SourceText = string.Empty;
        StatusText = Loc.T("StatusNoFile");
        FileSizeText = "—";
        GroupCountText = "—";
        ChannelCountText = "—";
        SampleCountText = "—";
        OnPropertyChanged(nameof(HasDocument));
    }

    /// <summary>Formats a byte count the way the stat tile shows it.</summary>
    /// <param name="bytes">Size in bytes.</param>
    /// <returns>For example <c>1.4 GB</c>.</returns>
    public static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var text = unit == 0
            ? value.ToString("0", CultureInfo.CurrentCulture)
            : value.ToString("0.#", CultureInfo.CurrentCulture);
        return $"{text} {units[unit]}";
    }

    /// <summary>Formats a sample count with thousands separators.</summary>
    /// <param name="count">Number of samples.</param>
    /// <returns>The formatted count.</returns>
    public static string FormatCount(long count) => count.ToString("N0", CultureInfo.CurrentCulture);

    private void Apply(string path, TdmsDocument document)
    {
        Document = document;
        FilePath = path;
        FileNameText = Path.GetFileName(path);

        Nodes.Clear();
        foreach (var node in TdmsNodeViewModel.BuildTree(document))
        {
            Nodes.Add(node);
        }

        SelectedNode = Nodes.FirstOrDefault();

        FileSizeText = FormatSize(document.SourceSizeBytes);
        GroupCountText = FormatCount(document.Groups.Count);
        ChannelCountText = FormatCount(document.ChannelCount);
        SampleCountText = FormatCount(document.TotalSampleCount);

        SourceText = document.ReadFromIndexFile ? Loc.T("StatusIndex") : Loc.T("StatusDataFile");
        StatusText = document.IsTruncated ? Loc.T("StatusTruncated") : Loc.T("StatusReady");
        ErrorMessage = document.IsTruncated ? Loc.T("StatusTruncated") : string.Empty;
        OnPropertyChanged(nameof(HasDocument));
    }

    partial void OnSelectedNodeChanged(TdmsNodeViewModel? value)
    {
        SelectedProperties.Clear();
        if (value is not null)
        {
            foreach (var row in value.Properties)
            {
                SelectedProperties.Add(row);
            }
        }

        OnPropertyChanged(nameof(SelectedTitle));
    }
}
