using Tdms.Core;

namespace Tdms.App.ViewModels;

/// <summary>One row of the property table: name, TDMS type and the value as text.</summary>
/// <param name="Name">Property name.</param>
/// <param name="Type">Short TDMS type name.</param>
/// <param name="Value">Culture independent value text.</param>
public sealed record PropertyRowViewModel(string Name, string Type, string Value)
{
    /// <summary>Builds a row from a TDMS property.</summary>
    /// <param name="name">Property name.</param>
    /// <param name="value">Property value.</param>
    /// <returns>The display row.</returns>
    public static PropertyRowViewModel From(string name, TdmsPropertyValue value) =>
        new(name, TdmsDataTypes.Name(value.DataType), value.ToInvariantString());
}
