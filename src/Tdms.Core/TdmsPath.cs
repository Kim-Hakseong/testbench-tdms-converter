using System.Text;

namespace Tdms.Core;

/// <summary>
/// TDMS object paths. The file object is <c>/</c>, a group is <c>/'group'</c> and a
/// channel is <c>/'group'/'channel'</c>; a single quote inside a name is doubled.
/// </summary>
public static class TdmsPath
{
    /// <summary>Path of the file-level object.</summary>
    public const string Root = "/";

    /// <summary>Splits a TDMS object path into its unescaped name components.</summary>
    /// <param name="path">Path as stored in the file.</param>
    /// <returns>Zero components for the file object, one for a group, two for a channel.</returns>
    /// <exception cref="TdmsFormatException">The path is not well formed.</exception>
    public static IReadOnlyList<string> Parse(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var parts = new List<string>();
        var i = 0;
        while (i < path.Length)
        {
            if (path[i] != '/')
            {
                throw new TdmsFormatException($"Malformed TDMS object path: {path}");
            }

            i++;
            if (i >= path.Length)
            {
                break;
            }

            if (path[i] != '\'')
            {
                throw new TdmsFormatException($"Malformed TDMS object path: {path}");
            }

            i++;
            var name = new StringBuilder();
            var closed = false;
            while (i < path.Length)
            {
                if (path[i] == '\'')
                {
                    if (i + 1 < path.Length && path[i + 1] == '\'')
                    {
                        name.Append('\'');
                        i += 2;
                        continue;
                    }

                    i++;
                    closed = true;
                    break;
                }

                name.Append(path[i]);
                i++;
            }

            if (!closed)
            {
                throw new TdmsFormatException($"Malformed TDMS object path: {path}");
            }

            parts.Add(name.ToString());
        }

        return parts;
    }

    /// <summary>Escapes one name component (doubles single quotes).</summary>
    /// <param name="name">Raw group or channel name.</param>
    /// <returns>The escaped name, without the surrounding quotes.</returns>
    public static string Escape(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Replace("'", "''", StringComparison.Ordinal);
    }

    /// <summary>Builds the path of a group object.</summary>
    /// <param name="group">Group name.</param>
    /// <returns>For example <c>/'Temperatures'</c>.</returns>
    public static string ForGroup(string group) => $"/'{Escape(group)}'";

    /// <summary>Builds the path of a channel object.</summary>
    /// <param name="group">Group name.</param>
    /// <param name="channel">Channel name.</param>
    /// <returns>For example <c>/'Temperatures'/'TC1'</c>.</returns>
    public static string ForChannel(string group, string channel) =>
        $"/'{Escape(group)}'/'{Escape(channel)}'";
}
