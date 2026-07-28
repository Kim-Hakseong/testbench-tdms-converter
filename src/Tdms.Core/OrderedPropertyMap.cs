using System.Collections;

namespace Tdms.Core;

/// <summary>
/// A small insertion-ordered string map. TDMS property order carries meaning for people
/// reading a file, so the reader never re-sorts it.
/// </summary>
public sealed class OrderedPropertyMap : IReadOnlyDictionary<string, TdmsPropertyValue>
{
    private readonly Dictionary<string, TdmsPropertyValue> _values = new(StringComparer.Ordinal);
    private readonly List<string> _order = [];

    /// <inheritdoc />
    public TdmsPropertyValue this[string key] => _values[key];

    /// <inheritdoc />
    public IEnumerable<string> Keys => _order;

    /// <inheritdoc />
    public IEnumerable<TdmsPropertyValue> Values => _order.Select(k => _values[k]);

    /// <inheritdoc />
    public int Count => _order.Count;

    /// <summary>Adds or replaces a property, keeping the position of an existing key.</summary>
    /// <param name="key">Property name.</param>
    /// <param name="value">Property value.</param>
    public void Set(string key, TdmsPropertyValue value)
    {
        if (!_values.ContainsKey(key))
        {
            _order.Add(key);
        }

        _values[key] = value;
    }

    /// <inheritdoc />
    public bool ContainsKey(string key) => _values.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, out TdmsPropertyValue value) => _values.TryGetValue(key, out value);

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, TdmsPropertyValue>> GetEnumerator()
    {
        foreach (var key in _order)
        {
            yield return new KeyValuePair<string, TdmsPropertyValue>(key, _values[key]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
