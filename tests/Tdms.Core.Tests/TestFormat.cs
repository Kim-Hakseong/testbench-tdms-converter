using System.Globalization;
using System.Text;
using Tdms.Core.Model;

namespace Tdms.Core.Tests;

/// <summary>Formatting helpers shared by the assertions.</summary>
public static class TestFormat
{
    /// <summary>Formats an expected value exactly the way the reader is required to.</summary>
    /// <param name="type">Channel data type.</param>
    /// <param name="value">Expected value as written by the test writer.</param>
    /// <returns>The invariant text.</returns>
    public static string Text(TdmsDataType type, object value) => type switch
    {
        TdmsDataType.String => (string)value,
        TdmsDataType.Timestamp => ((TdmsTimestamp)value).ToString(),
        TdmsDataType.Boolean => (bool)value ? "1" : "0",
        TdmsDataType.F32 => ((float)value).ToString(CultureInfo.InvariantCulture),
        TdmsDataType.F64 => ((double)value).ToString(CultureInfo.InvariantCulture),
        _ when TdmsDataTypes.IsUnsigned(type) =>
            Convert.ToUInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
        _ => Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// A complete, canonical text rendering of a document — tree, types, counts, every
    /// property and every sample. Two reads are identical exactly when these strings are.
    /// </summary>
    /// <param name="document">Document to dump.</param>
    /// <returns>The canonical dump.</returns>
    public static string Dump(TdmsDocument document)
    {
        var text = new StringBuilder();
        text.Append("truncated=").Append(document.IsTruncated).Append('\n');
        foreach (var property in document.Properties)
        {
            text.Append("file\t").Append(property.Key).Append('\t')
                .Append(TdmsDataTypes.Name(property.Value.DataType)).Append('\t')
                .Append(property.Value.ToInvariantString()).Append('\n');
        }

        foreach (var group in document.Groups)
        {
            text.Append("group\t").Append(group.Name).Append('\n');
            foreach (var property in group.Properties)
            {
                text.Append("  prop\t").Append(property.Key).Append('\t')
                    .Append(TdmsDataTypes.Name(property.Value.DataType)).Append('\t')
                    .Append(property.Value.ToInvariantString()).Append('\n');
            }

            foreach (var channel in group.Channels)
            {
                text.Append("  channel\t").Append(channel.Name).Append('\t')
                    .Append(TdmsDataTypes.Name(channel.DataType)).Append('\t')
                    .Append(channel.SampleCount.ToString(CultureInfo.InvariantCulture)).Append('\n');
                foreach (var property in channel.Properties)
                {
                    text.Append("    prop\t").Append(property.Key).Append('\t')
                        .Append(TdmsDataTypes.Name(property.Value.DataType)).Append('\t')
                        .Append(property.Value.ToInvariantString()).Append('\n');
                }

                if (channel.Data is not { } data)
                {
                    continue;
                }

                for (var i = 0; i < data.Count; i++)
                {
                    text.Append("    v\t").Append(i.ToString(CultureInfo.InvariantCulture)).Append('\t')
                        .Append(data.GetText(i)).Append('\n');
                }
            }
        }

        return text.ToString();
    }
}
