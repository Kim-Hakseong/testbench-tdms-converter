using System.Buffers.Binary;
using System.Text;
using Tdms.Core.Model;

namespace Tdms.Core;

/// <summary>
/// Push based TDMS parser. Feed it arbitrary byte chunks and it advances a small internal
/// window; the whole file is never required in memory and the result is independent of how
/// the bytes were sliced.
/// </summary>
/// <remarks>
/// <para>
/// Supported: little-endian segments, non-interleaved raw data, the numeric types
/// <c>i8…i64</c>, <c>u8…u64</c>, <c>f32</c>, <c>f64</c>, plus <c>bool</c>, <c>string</c> and
/// TDMS timestamps as both properties and channel data; incremental segments that reuse the
/// previous raw index; several chunks per segment; <c>.tdms_index</c> sidecars (<c>TDSh</c>
/// segments); and a truncated final segment.
/// </para>
/// <para>
/// Rejected with a clear error instead of wrong numbers: big-endian segments, interleaved
/// raw data and DAQmx raw data (both the format-changing scaler and the digital line scaler
/// index).
/// </para>
/// </remarks>
public sealed class TdmsStreamParser
{
    /// <summary>Lead-in tag of a data file segment, read big-endian: <c>TDSm</c>.</summary>
    public const uint SegmentTag = 0x5444536D;

    /// <summary>Lead-in tag of a <c>.tdms_index</c> segment, read big-endian: <c>TDSh</c>.</summary>
    public const uint IndexSegmentTag = 0x54445368;

    /// <summary>Length of a segment lead-in in bytes.</summary>
    public const int LeadInLength = 28;

    private const uint TocMetaData = 1u << 1;
    private const uint TocNewObjectList = 1u << 2;
    private const uint TocRawData = 1u << 3;
    private const uint TocInterleavedData = 1u << 5;
    private const uint TocBigEndian = 1u << 6;
    private const uint TocDaqmxRawData = 1u << 7;

    private const uint NoRawDataIndex = 0xFFFFFFFF;
    private const uint SameRawDataIndex = 0x00000000;
    private const uint DaqmxFormatChangingScaler = 0x69120000;
    private const uint DaqmxDigitalLineScaler = 0x69130000;

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    private readonly TdmsReadOptions _options;
    private readonly Dictionary<string, ObjectState> _objects = new(StringComparer.Ordinal);
    private readonly List<ObjectState> _objectOrder = [];
    private readonly List<ObjectState> _rawList = [];

    private byte[] _buffer = new byte[64 * 1024];
    private int _start;
    private int _end;

    private State _state = State.LeadIn;
    private uint _toc;
    private int _metaLength;
    private long _rawBytesLeft;
    private long _chunkBytes;
    private int _rawObjectIndex;
    private long _valuesLeft;
    private long _objectBytesLeft;
    private long _partialBytes;
    private int _segmentCount;
    private bool _truncated;
    private bool _indexFile;
    private bool _finished;
    private TdmsDocument? _result;

    /// <summary>Creates a parser.</summary>
    /// <param name="options">Read settings; defaults to a full in-memory read.</param>
    public TdmsStreamParser(TdmsReadOptions? options = null) => _options = options ?? new TdmsReadOptions();

    private enum State
    {
        LeadIn,
        Metadata,
        RawData,
    }

    /// <summary>Whether the bytes seen so far came from a <c>.tdms_index</c> sidecar.</summary>
    public bool IsIndexFile => _indexFile;

    /// <summary>Number of segments read so far.</summary>
    public int SegmentCount => _segmentCount;

    /// <summary>
    /// Raw bytes a seekable caller may skip right now instead of reading them: a positive
    /// count, <c>-1</c> when the current segment is the truncated final one (skip to the end
    /// of the file), or <c>0</c> when nothing may be skipped.
    /// </summary>
    /// <remarks>Only ever non-zero in <see cref="TdmsReadMode.MetadataOnly"/>.</remarks>
    public long PendingSkipBytes =>
        _options.Mode == TdmsReadMode.MetadataOnly && _state == State.RawData && _end == _start && !_finished
            ? _rawBytesLeft
            : 0;

    /// <summary>Feeds the next bytes of the file.</summary>
    /// <param name="data">Any number of bytes, in file order.</param>
    /// <exception cref="InvalidOperationException">Called after <see cref="Finish"/>.</exception>
    public void Push(ReadOnlySpan<byte> data)
    {
        if (_finished)
        {
            throw new InvalidOperationException("Push() was called after Finish().");
        }

        Append(data);
        Process();
    }

    /// <summary>
    /// Declares that <paramref name="byteCount"/> raw bytes were skipped in the underlying
    /// stream. Sample counts are still updated.
    /// </summary>
    /// <param name="byteCount">Number of bytes skipped.</param>
    /// <exception cref="InvalidOperationException">The parser is not inside a raw section.</exception>
    public void Skip(long byteCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteCount);
        if (_finished)
        {
            throw new InvalidOperationException("Skip() was called after Finish().");
        }

        if (_state != State.RawData)
        {
            throw new InvalidOperationException("Skip() is only valid inside a raw data section.");
        }

        if (_rawBytesLeft < 0)
        {
            // Truncated final segment: the caller tells us how much really was there.
            _rawBytesLeft = byteCount;
            _truncated = true;
        }

        SkipRawBytes(byteCount);
        if (_rawBytesLeft == 0)
        {
            EndSegment();
        }

        Process();
    }

    /// <summary>Completes the read and builds the document.</summary>
    /// <returns>The parsed file. Calling this twice returns the same instance.</returns>
    /// <exception cref="TdmsFormatException">The file ends inside a lead-in or metadata block
    /// and <see cref="TdmsReadOptions.TolerateTruncatedTail"/> is off.</exception>
    public TdmsDocument Finish()
    {
        if (_result is not null)
        {
            return _result;
        }

        var leftover = _end - _start;
        if (_state == State.Metadata || (_state == State.LeadIn && leftover > 0))
        {
            if (!_options.TolerateTruncatedTail)
            {
                throw new TdmsFormatException(
                    "Truncated TDMS file: the last segment header or metadata block is incomplete.");
            }

            _truncated = true;
        }

        if (_state == State.RawData && _rawBytesLeft != 0)
        {
            _truncated = true;
        }

        _finished = true;
        _options.Sink?.OnFinished();
        _result = BuildDocument();
        return _result;
    }

    private ReadOnlySpan<byte> Window => _buffer.AsSpan(_start, _end - _start);

    private int Available => _end - _start;

    private void Append(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
        {
            return;
        }

        var available = _end - _start;
        if (_end + data.Length > _buffer.Length)
        {
            var needed = available + data.Length;
            if (needed <= _buffer.Length)
            {
                Array.Copy(_buffer, _start, _buffer, 0, available);
            }
            else
            {
                var capacity = _buffer.Length;
                while (capacity < needed)
                {
                    capacity *= 2;
                }

                var grown = new byte[capacity];
                Array.Copy(_buffer, _start, grown, 0, available);
                _buffer = grown;
            }

            _start = 0;
            _end = available;
        }

        data.CopyTo(_buffer.AsSpan(_end));
        _end += data.Length;
    }

    private void Discard(int count)
    {
        _start += count;
        if (_start == _end)
        {
            _start = 0;
            _end = 0;
        }
    }

    private void Process()
    {
        while (true)
        {
            switch (_state)
            {
                case State.LeadIn:
                    // Reject a wrong tag as soon as the first bytes arrive, so a file that is
                    // not TDMS at all fails loudly instead of parsing as an empty document.
                    ValidateTagPrefix();
                    if (Available < LeadInLength)
                    {
                        return;
                    }

                    ReadLeadIn();
                    continue;

                case State.Metadata:
                    if (Available < _metaLength)
                    {
                        return;
                    }

                    ParseMetadata(_buffer.AsSpan(_start, _metaLength));
                    Discard(_metaLength);
                    _state = State.RawData;
                    StartRawSection();
                    continue;

                case State.RawData:
                default:
                    if (!PumpRawSection())
                    {
                        return;
                    }

                    EndSegment();
                    continue;
            }
        }
    }

    /// <summary>Checks whatever part of the four byte tag has arrived so far.</summary>
    private void ValidateTagPrefix()
    {
        ReadOnlySpan<byte> tag = "TDSm"u8;
        var window = Window;
        var length = Math.Min(4, window.Length);
        for (var i = 0; i < length; i++)
        {
            // The sidecar differs only in the last byte: TDSh.
            var expected = i == 3 && window[i] == (byte)'h' ? (byte)'h' : tag[i];
            if (window[i] != expected)
            {
                throw new TdmsFormatException(
                    "Not a TDMS file: the segment does not start with the 'TDSm' (or 'TDSh') tag.");
            }
        }
    }

    private void ReadLeadIn()
    {
        var head = Window[..LeadInLength];
        var tag = BinaryPrimitives.ReadUInt32BigEndian(head);
        if (tag == IndexSegmentTag)
        {
            _indexFile = true;
        }
        else if (tag != SegmentTag)
        {
            throw new TdmsFormatException(
                "Not a TDMS file: the segment does not start with the 'TDSm' (or 'TDSh') tag.");
        }

        _toc = BinaryPrimitives.ReadUInt32LittleEndian(head[4..]);
        RejectUnsupportedToc(_toc);

        var nextSegmentOffset = BinaryPrimitives.ReadUInt64LittleEndian(head[12..]);
        var rawDataOffset = BinaryPrimitives.ReadUInt64LittleEndian(head[20..]);
        Discard(LeadInLength);
        _segmentCount++;

        var truncated = nextSegmentOffset == ulong.MaxValue;
        var metaLength = (_toc & TocMetaData) != 0 ? rawDataOffset : 0;
        if (metaLength > int.MaxValue)
        {
            throw new TdmsFormatException("The TDMS metadata block is larger than 2 GB.");
        }

        if (!truncated && nextSegmentOffset < rawDataOffset)
        {
            throw new TdmsFormatException(
                "Malformed TDMS lead-in: the next segment offset precedes the raw data offset.");
        }

        _metaLength = (int)metaLength;
        if ((_toc & TocRawData) == 0)
        {
            _rawBytesLeft = 0;
        }
        else
        {
            _rawBytesLeft = truncated ? -1 : (long)(nextSegmentOffset - rawDataOffset);
            if (truncated)
            {
                _truncated = true;
            }
        }

        if (_metaLength > 0)
        {
            _state = State.Metadata;
        }
        else
        {
            _state = State.RawData;
            StartRawSection();
        }
    }

    private static void RejectUnsupportedToc(uint toc)
    {
        if ((toc & TocBigEndian) != 0)
        {
            throw new TdmsUnsupportedFeatureException(
                "Big-endian TDMS segments are not supported (lead-in ToC bit kTocBigEndian is set).");
        }

        if ((toc & TocDaqmxRawData) != 0)
        {
            throw new TdmsUnsupportedFeatureException(
                "DAQmx raw data is not supported (lead-in ToC bit kTocDAQmxRawData is set).");
        }

        if ((toc & TocInterleavedData) != 0)
        {
            throw new TdmsUnsupportedFeatureException(
                "Interleaved raw data is not supported (lead-in ToC bit kTocInterleavedData is set).");
        }
    }

    private void EndSegment()
    {
        _rawBytesLeft = 0;
        _state = State.LeadIn;
    }

    private void ParseMetadata(ReadOnlySpan<byte> meta)
    {
        var offset = 0;
        var newObjectList = (_toc & TocNewObjectList) != 0;
        if (newObjectList)
        {
            _rawList.Clear();
        }

        var objectCount = ReadUInt32(meta, ref offset);
        for (var i = 0; i < objectCount; i++)
        {
            var path = ReadString(meta, ref offset);
            var state = GetOrCreateObject(path);
            var header = ReadUInt32(meta, ref offset);

            switch (header)
            {
                case NoRawDataIndex:
                    break;

                case DaqmxFormatChangingScaler:
                case DaqmxDigitalLineScaler:
                    throw new TdmsUnsupportedFeatureException(
                        $"DAQmx raw data is not supported (object {path} carries a DAQmx raw data index).");

                case SameRawDataIndex:
                    if (state.Index is null)
                    {
                        throw new TdmsFormatException(
                            $"Object {path} reuses the previous raw index but never declared one.");
                    }

                    AddToRawList(state, newObjectList);
                    break;

                default:
                    ReadRawIndex(meta, ref offset, state, path);
                    AddToRawList(state, newObjectList);
                    break;
            }

            var propertyCount = ReadUInt32(meta, ref offset);
            for (var p = 0; p < propertyCount; p++)
            {
                var name = ReadString(meta, ref offset);
                var value = ReadPropertyValue(meta, ref offset, name);
                state.Properties.Set(name, value);
            }
        }
    }

    private void ReadRawIndex(ReadOnlySpan<byte> meta, ref int offset, ObjectState state, string path)
    {
        var dataType = (TdmsDataType)ReadUInt32(meta, ref offset);
        var dimension = ReadUInt32(meta, ref offset);
        if (dimension != 1)
        {
            throw new TdmsUnsupportedFeatureException(
                $"Only one-dimensional TDMS channels are supported ({path} declares {dimension} dimensions).");
        }

        var count = (long)ReadUInt64(meta, ref offset);
        long totalBytes;
        if (dataType == TdmsDataType.String)
        {
            totalBytes = (long)ReadUInt64(meta, ref offset);
        }
        else if (TdmsDataTypes.TryGetFixedSize(dataType, out var size))
        {
            totalBytes = count * size;
        }
        else
        {
            throw new TdmsUnsupportedFeatureException(
                $"Channel {path} uses the unsupported raw data type {TdmsDataTypes.Name(dataType)}.");
        }

        if (state.Index is { } previous && previous.DataType != dataType)
        {
            throw new TdmsUnsupportedFeatureException(
                $"Channel {path} changes its data type from {TdmsDataTypes.Name(previous.DataType)} " +
                $"to {TdmsDataTypes.Name(dataType)} mid-file, which this reader does not model.");
        }

        state.Index = new RawIndex(dataType, count, totalBytes);
        state.Reference.DataType = dataType;

        if (ShouldDecode(state))
        {
            state.Block ??= new TdmsSampleBuffer(dataType);
            if (_options.StoreValues)
            {
                state.Data ??= new TdmsSampleBuffer(dataType);
            }
        }
    }

    private bool ShouldDecode(ObjectState state) =>
        _options.Mode == TdmsReadMode.Full &&
        (_options.ChannelFilter is null || _options.ChannelFilter.Contains(state.Path));

    private void AddToRawList(ObjectState state, bool newObjectList)
    {
        if (newObjectList || !_rawList.Contains(state))
        {
            _rawList.Add(state);
        }
    }

    private ObjectState GetOrCreateObject(string path)
    {
        if (_objects.TryGetValue(path, out var existing))
        {
            return existing;
        }

        var parts = TdmsPath.Parse(path);
        if (parts.Count > 2)
        {
            throw new TdmsUnsupportedFeatureException(
                $"TDMS object path {path} is deeper than group/channel, which this reader does not model.");
        }

        var group = parts.Count >= 1 ? parts[0] : string.Empty;
        var name = parts.Count >= 2 ? parts[1] : string.Empty;
        var created = new ObjectState(path, new TdmsChannelRef(path, group, name), parts.Count);
        _objects[path] = created;
        _objectOrder.Add(created);
        return created;
    }

    /// <summary>Positions the raw cursor at the first object of the segment's first chunk.</summary>
    private void StartRawSection()
    {
        _rawObjectIndex = 0;
        _valuesLeft = 0;
        _objectBytesLeft = 0;
        _chunkBytes = 0;
        foreach (var state in _rawList)
        {
            _chunkBytes += state.Index!.Value.TotalBytes;
        }

        if (_rawBytesLeft != 0 && _chunkBytes > 0)
        {
            SetCursor(_rawList[0]);
        }

        if (_indexFile)
        {
            // A .tdms_index sidecar describes the data file's raw section without carrying it.
            if (_rawBytesLeft > 0 && _chunkBytes > 0)
            {
                SkipRawBytes(_rawBytesLeft);
            }

            EndSegment();
        }
    }

    private void SetCursor(ObjectState state)
    {
        var index = state.Index ?? throw new TdmsFormatException(
            $"Object {state.Path} is in the raw data list without a raw index.");
        _valuesLeft = index.Count;
        _objectBytesLeft = index.TotalBytes;
        _partialBytes = 0;
    }

    /// <summary>Steps to the next object of the current chunk.</summary>
    private void AdvanceObject()
    {
        _rawObjectIndex++;
        if (_rawObjectIndex < _rawList.Count)
        {
            SetCursor(_rawList[_rawObjectIndex]);
        }
        else
        {
            _options.Sink?.OnChunkCompleted();
        }
    }

    /// <summary>Rewinds the cursor for another chunk. False when the raw section is finished.</summary>
    private bool StartNextChunk()
    {
        if (_rawBytesLeft == 0 || _chunkBytes <= 0)
        {
            return false;
        }

        _rawObjectIndex = 0;
        SetCursor(_rawList[0]);
        return true;
    }

    /// <summary>Accounts for raw bytes that were never materialised. Returns the bytes consumed.</summary>
    private long SkipRawBytes(long byteCount)
    {
        if (_chunkBytes <= 0)
        {
            if (_rawBytesLeft <= 0)
            {
                return byteCount;
            }

            var dropped = Math.Min(byteCount, _rawBytesLeft);
            _rawBytesLeft -= dropped;
            return dropped;
        }

        var remaining = byteCount;
        while (remaining > 0 && _rawBytesLeft != 0)
        {
            if (_rawObjectIndex >= _rawList.Count && !StartNextChunk())
            {
                break;
            }

            var state = _rawList[_rawObjectIndex];
            var take = Math.Min(remaining, _objectBytesLeft);
            if (_rawBytesLeft > 0)
            {
                take = Math.Min(take, _rawBytesLeft);
            }

            CountSkipped(state, take);
            _objectBytesLeft -= take;
            remaining -= take;
            if (_rawBytesLeft > 0)
            {
                _rawBytesLeft -= take;
            }

            if (_objectBytesLeft == 0)
            {
                AdvanceObject();
            }
        }

        return byteCount - remaining;
    }

    private void CountSkipped(ObjectState state, long bytes)
    {
        var index = state.Index!.Value;
        if (index.DataType == TdmsDataType.String)
        {
            // A string block only yields its values when it is skipped whole.
            if (bytes == _objectBytesLeft)
            {
                state.SampleCount += _valuesLeft;
                _valuesLeft = 0;
            }

            return;
        }

        // Skips do not have to land on a value boundary, so carry the remainder over.
        TdmsDataTypes.TryGetFixedSize(index.DataType, out var size);
        var total = _partialBytes + bytes;
        var values = Math.Min(_valuesLeft, total / size);
        _partialBytes = total - (values * size);
        state.SampleCount += values;
        _valuesLeft -= values;
    }

    /// <summary>Consumes the raw section of the current segment. True when the section is complete.</summary>
    private bool PumpRawSection()
    {
        while (true)
        {
            if (_rawBytesLeft == 0)
            {
                return true;
            }

            if (_chunkBytes <= 0)
            {
                // Raw bytes without a decodable object list: drop them.
                if (_rawBytesLeft < 0)
                {
                    Discard(Available);
                    return false;
                }

                var drop = (int)Math.Min(Available, _rawBytesLeft);
                Discard(drop);
                _rawBytesLeft -= drop;
                return _rawBytesLeft == 0;
            }

            if (_options.Mode == TdmsReadMode.MetadataOnly)
            {
                var amount = _rawBytesLeft < 0 ? Available : Math.Min(Available, _rawBytesLeft);
                if (amount == 0)
                {
                    return _rawBytesLeft == 0;
                }

                var consumed = SkipRawBytes(amount);
                Discard((int)consumed);
                if (consumed == 0)
                {
                    return _rawBytesLeft == 0;
                }

                continue;
            }

            if (_rawObjectIndex >= _rawList.Count && !StartNextChunk())
            {
                return true;
            }

            var state = _rawList[_rawObjectIndex];
            if (_objectBytesLeft == 0)
            {
                AdvanceObject();
                continue;
            }

            if (!DecodeFromObject(state))
            {
                return false;
            }
        }
    }

    /// <summary>Decodes what is currently buffered for one object. False means "need more bytes".</summary>
    private bool DecodeFromObject(ObjectState state)
    {
        var index = state.Index!.Value;
        var decode = ShouldDecode(state);

        if (index.DataType == TdmsDataType.String)
        {
            var blockBytes = _objectBytesLeft;
            if (_rawBytesLeft > 0)
            {
                blockBytes = Math.Min(blockBytes, _rawBytesLeft);
            }

            if (blockBytes > int.MaxValue)
            {
                throw new TdmsUnsupportedFeatureException(
                    $"String channel {state.Path} declares a chunk larger than 2 GB.");
            }

            if (Available < blockBytes)
            {
                return false;
            }

            // A block clipped by the end of the raw section cannot be decoded — drop it.
            var partial = blockBytes < _objectBytesLeft;
            var count = _valuesLeft;
            if (decode && !partial)
            {
                DecodeStrings(state, _buffer.AsSpan(_start, (int)blockBytes), count);
            }

            Discard((int)blockBytes);
            state.SampleCount += partial ? 0 : count;
            _valuesLeft = 0;
            _objectBytesLeft -= blockBytes;
            if (_rawBytesLeft > 0)
            {
                _rawBytesLeft -= blockBytes;
            }

            AdvanceObject();
            return true;
        }

        TdmsDataTypes.TryGetFixedSize(index.DataType, out var size);
        var byBytes = _rawBytesLeft < 0 ? long.MaxValue : _rawBytesLeft / size;
        var values = Math.Min(Math.Min(Available / size, _valuesLeft), byBytes);
        if (values == 0)
        {
            if (_rawBytesLeft > 0 && _rawBytesLeft < size)
            {
                // Trailing bytes that cannot form a whole value: drop the incomplete tail.
                var drop = (int)Math.Min(Available, _rawBytesLeft);
                if (drop == 0)
                {
                    return false;
                }

                Discard(drop);
                _rawBytesLeft -= drop;
                return true;
            }

            return false;
        }

        var consumed = (int)(values * size);
        if (decode)
        {
            DecodeNumeric(state, _buffer.AsSpan(_start, consumed), index.DataType, (int)values);
        }

        Discard(consumed);
        state.SampleCount += values;
        _valuesLeft -= values;
        _objectBytesLeft -= consumed;
        if (_rawBytesLeft > 0)
        {
            _rawBytesLeft -= consumed;
        }

        if (_valuesLeft == 0)
        {
            AdvanceObject();
        }

        return true;
    }

    private void DecodeNumeric(ObjectState state, ReadOnlySpan<byte> bytes, TdmsDataType type, int count)
    {
        var block = state.Block!;
        block.Clear();
        var offset = 0;
        for (var i = 0; i < count; i++)
        {
            switch (type)
            {
                case TdmsDataType.I8:
                    block.AddLong((sbyte)bytes[offset]);
                    offset += 1;
                    break;
                case TdmsDataType.U8:
                    block.AddULong(bytes[offset]);
                    offset += 1;
                    break;
                case TdmsDataType.Boolean:
                    block.AddLong(bytes[offset] != 0 ? 1 : 0);
                    offset += 1;
                    break;
                case TdmsDataType.I16:
                    block.AddLong(BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..]));
                    offset += 2;
                    break;
                case TdmsDataType.U16:
                    block.AddULong(BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]));
                    offset += 2;
                    break;
                case TdmsDataType.I32:
                    block.AddLong(BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]));
                    offset += 4;
                    break;
                case TdmsDataType.U32:
                    block.AddULong(BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]));
                    offset += 4;
                    break;
                case TdmsDataType.I64:
                    block.AddLong(BinaryPrimitives.ReadInt64LittleEndian(bytes[offset..]));
                    offset += 8;
                    break;
                case TdmsDataType.U64:
                    block.AddULong(BinaryPrimitives.ReadUInt64LittleEndian(bytes[offset..]));
                    offset += 8;
                    break;
                case TdmsDataType.F32:
                    block.AddDouble(BinaryPrimitives.ReadSingleLittleEndian(bytes[offset..]));
                    offset += 4;
                    break;
                case TdmsDataType.F64:
                    block.AddDouble(BinaryPrimitives.ReadDoubleLittleEndian(bytes[offset..]));
                    offset += 8;
                    break;
                case TdmsDataType.Timestamp:
                    block.AddTimestamp(new TdmsTimestamp(
                        BinaryPrimitives.ReadInt64LittleEndian(bytes[(offset + 8)..]),
                        BinaryPrimitives.ReadUInt64LittleEndian(bytes[offset..])));
                    offset += 16;
                    break;
                default:
                    throw new TdmsUnsupportedFeatureException(
                        $"Channel {state.Path} uses the unsupported raw data type {TdmsDataTypes.Name(type)}.");
            }
        }

        Publish(state, block);
    }

    private void DecodeStrings(ObjectState state, ReadOnlySpan<byte> bytes, long count)
    {
        var block = state.Block!;
        block.Clear();
        var offsetTableBytes = count * 4;
        if (offsetTableBytes > bytes.Length)
        {
            throw new TdmsFormatException($"String channel {state.Path} has a truncated offset table.");
        }

        var data = bytes[(int)offsetTableBytes..];
        var previous = 0u;
        for (var i = 0; i < count; i++)
        {
            var end = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(i * 4)..]);
            if (end < previous || end > data.Length)
            {
                throw new TdmsFormatException($"String channel {state.Path} has an out-of-range string offset.");
            }

            block.AddString(Utf8.GetString(data[(int)previous..(int)end]));
            previous = end;
        }

        Publish(state, block);
    }

    private void Publish(ObjectState state, TdmsSampleBuffer block)
    {
        _options.Sink?.OnSamples(state.Reference, block);
        state.Data?.Append(block);
    }

    private static TdmsPropertyValue ReadPropertyValue(ReadOnlySpan<byte> meta, ref int offset, string name)
    {
        var type = (TdmsDataType)ReadUInt32(meta, ref offset);
        switch (type)
        {
            case TdmsDataType.I8:
                return new TdmsPropertyValue(type, (long)(sbyte)ReadByte(meta, ref offset));
            case TdmsDataType.U8:
                return new TdmsPropertyValue(type, (ulong)ReadByte(meta, ref offset));
            case TdmsDataType.Boolean:
                return new TdmsPropertyValue(type, ReadByte(meta, ref offset) != 0);
            case TdmsDataType.I16:
                return new TdmsPropertyValue(type, (long)BinaryPrimitives.ReadInt16LittleEndian(Take(meta, ref offset, 2)));
            case TdmsDataType.U16:
                return new TdmsPropertyValue(type, (ulong)BinaryPrimitives.ReadUInt16LittleEndian(Take(meta, ref offset, 2)));
            case TdmsDataType.I32:
                return new TdmsPropertyValue(type, (long)BinaryPrimitives.ReadInt32LittleEndian(Take(meta, ref offset, 4)));
            case TdmsDataType.U32:
                return new TdmsPropertyValue(type, (ulong)BinaryPrimitives.ReadUInt32LittleEndian(Take(meta, ref offset, 4)));
            case TdmsDataType.I64:
                return new TdmsPropertyValue(type, BinaryPrimitives.ReadInt64LittleEndian(Take(meta, ref offset, 8)));
            case TdmsDataType.U64:
                return new TdmsPropertyValue(type, BinaryPrimitives.ReadUInt64LittleEndian(Take(meta, ref offset, 8)));
            case TdmsDataType.F32:
                return new TdmsPropertyValue(type, (double)BinaryPrimitives.ReadSingleLittleEndian(Take(meta, ref offset, 4)));
            case TdmsDataType.F64:
                return new TdmsPropertyValue(type, BinaryPrimitives.ReadDoubleLittleEndian(Take(meta, ref offset, 8)));
            case TdmsDataType.String:
                return new TdmsPropertyValue(type, ReadString(meta, ref offset));
            case TdmsDataType.Timestamp:
            {
                var block = Take(meta, ref offset, 16);
                return new TdmsPropertyValue(type, new TdmsTimestamp(
                    BinaryPrimitives.ReadInt64LittleEndian(block[8..]),
                    BinaryPrimitives.ReadUInt64LittleEndian(block)));
            }

            default:
                throw new TdmsUnsupportedFeatureException(
                    $"Property \"{name}\" uses the unsupported data type {TdmsDataTypes.Name(type)}.");
        }
    }

    private static ReadOnlySpan<byte> Take(ReadOnlySpan<byte> span, ref int offset, int count)
    {
        if (offset + count > span.Length)
        {
            throw new TdmsFormatException("The TDMS metadata block ends unexpectedly.");
        }

        var slice = span.Slice(offset, count);
        offset += count;
        return slice;
    }

    private static byte ReadByte(ReadOnlySpan<byte> span, ref int offset) => Take(span, ref offset, 1)[0];

    private static uint ReadUInt32(ReadOnlySpan<byte> span, ref int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(Take(span, ref offset, 4));

    private static ulong ReadUInt64(ReadOnlySpan<byte> span, ref int offset) =>
        BinaryPrimitives.ReadUInt64LittleEndian(Take(span, ref offset, 8));

    private static string ReadString(ReadOnlySpan<byte> span, ref int offset)
    {
        var length = ReadUInt32(span, ref offset);
        if (length > int.MaxValue)
        {
            throw new TdmsFormatException("A TDMS string claims to be longer than 2 GB.");
        }

        return Utf8.GetString(Take(span, ref offset, (int)length));
    }

    private TdmsDocument BuildDocument()
    {
        var fileProperties = new OrderedPropertyMap();
        var groupProperties = new Dictionary<string, OrderedPropertyMap>(StringComparer.Ordinal);
        var groupChannels = new Dictionary<string, List<TdmsChannel>>(StringComparer.Ordinal);
        var groupPaths = new Dictionary<string, string>(StringComparer.Ordinal);
        var groupOrder = new List<string>();

        void EnsureGroup(string name, string path)
        {
            if (groupChannels.ContainsKey(name))
            {
                return;
            }

            groupChannels[name] = [];
            groupProperties[name] = new OrderedPropertyMap();
            groupPaths[name] = path;
            groupOrder.Add(name);
        }

        foreach (var state in _objectOrder)
        {
            switch (state.Depth)
            {
                case 0:
                    fileProperties = state.Properties;
                    break;

                case 1:
                    EnsureGroup(state.Reference.GroupName, state.Path);
                    groupProperties[state.Reference.GroupName] = state.Properties;
                    break;

                default:
                {
                    var group = state.Reference.GroupName;
                    EnsureGroup(group, TdmsPath.ForGroup(group));
                    groupChannels[group].Add(new TdmsChannel(
                        state.Path,
                        group,
                        state.Reference.Name,
                        state.Index?.DataType ?? TdmsDataType.Void,
                        state.SampleCount,
                        state.Properties,
                        state.Data));
                    break;
                }
            }
        }

        var groups = groupOrder
            .Select(name => new TdmsGroup(groupPaths[name], name, groupProperties[name], groupChannels[name]))
            .ToList();

        return new TdmsDocument(fileProperties, groups, _segmentCount, _truncated);
    }

    private readonly record struct RawIndex(TdmsDataType DataType, long Count, long TotalBytes);

    private sealed class ObjectState(string path, TdmsChannelRef reference, int depth)
    {
        public string Path { get; } = path;

        public TdmsChannelRef Reference { get; } = reference;

        public int Depth { get; } = depth;

        public OrderedPropertyMap Properties { get; } = new();

        public RawIndex? Index { get; set; }

        public long SampleCount { get; set; }

        public TdmsSampleBuffer? Data { get; set; }

        public TdmsSampleBuffer? Block { get; set; }
    }
}
