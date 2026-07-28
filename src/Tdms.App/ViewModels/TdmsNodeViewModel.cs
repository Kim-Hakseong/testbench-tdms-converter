using System.Collections.ObjectModel;
using System.Globalization;
using Tdms.App.Localization;
using Tdms.Core;
using Tdms.Core.Model;

namespace Tdms.App.ViewModels;

/// <summary>What kind of TDMS object a tree node stands for.</summary>
public enum TdmsNodeKind
{
    /// <summary>The file level object.</summary>
    File,

    /// <summary>A group.</summary>
    Group,

    /// <summary>A channel.</summary>
    Channel,
}

/// <summary>A node of the group/channel tree.</summary>
public sealed class TdmsNodeViewModel
{
    private TdmsNodeViewModel(
        TdmsNodeKind kind,
        string path,
        string name,
        string detail,
        IReadOnlyDictionary<string, TdmsPropertyValue> properties)
    {
        Kind = kind;
        Path = path;
        Name = name;
        Detail = detail;
        Properties = new ObservableCollection<PropertyRowViewModel>(
            properties.Select(p => PropertyRowViewModel.From(p.Key, p.Value)));
    }

    /// <summary>What the node stands for.</summary>
    public TdmsNodeKind Kind { get; }

    /// <summary>TDMS object path.</summary>
    public string Path { get; }

    /// <summary>Display name.</summary>
    public string Name { get; }

    /// <summary>Secondary line: data type and sample count, or the channel count of a group.</summary>
    public string Detail { get; }

    /// <summary>Properties of this object.</summary>
    public ObservableCollection<PropertyRowViewModel> Properties { get; }

    /// <summary>Child nodes; empty for channels.</summary>
    public ObservableCollection<TdmsNodeViewModel> Children { get; } = [];

    /// <summary>Whether the node shows an expander in the tree.</summary>
    public bool IsExpanded { get; set; } = true;

    /// <summary>Builds the tree of a document, with the file object as the root node.</summary>
    /// <param name="document">Parsed document.</param>
    /// <returns>The root nodes.</returns>
    public static ObservableCollection<TdmsNodeViewModel> BuildTree(TdmsDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var fileName = document.SourcePath is { } path ? System.IO.Path.GetFileName(path) : Loc.T("FileNode");
        var root = new TdmsNodeViewModel(
            TdmsNodeKind.File,
            TdmsPath.Root,
            fileName,
            string.Format(
                CultureInfo.CurrentCulture,
                "{0} {1} · {2} {3}",
                document.SegmentCount,
                Loc.T("UnitSegments"),
                document.ChannelCount,
                Loc.T("UnitChannels")),
            document.Properties);

        foreach (var group in document.Groups)
        {
            var groupNode = new TdmsNodeViewModel(
                TdmsNodeKind.Group,
                group.Path,
                group.Name,
                string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} {1}",
                    group.Channels.Count,
                    Loc.T("UnitChannels")),
                group.Properties);

            foreach (var channel in group.Channels)
            {
                groupNode.Children.Add(new TdmsNodeViewModel(
                    TdmsNodeKind.Channel,
                    channel.Path,
                    channel.Name,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "{0} · {1} {2}",
                        TdmsDataTypes.Name(channel.DataType),
                        channel.SampleCount.ToString("N0", CultureInfo.CurrentCulture),
                        Loc.T("UnitSamples")),
                    channel.Properties));
            }

            root.Children.Add(groupNode);
        }

        return [root];
    }
}
