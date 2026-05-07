using System.Buffers.Binary;

namespace CarrotFantasyServer.Protocol;

/// <summary>
/// 在「负载」字节数组内按顺序读字段，便于与客户端约定同一套字段顺序与 endianness（默认小端）。
/// </summary>
public sealed class BinaryPayloadReader
{
    private readonly byte[] _buffer;
    private int _index;

    public BinaryPayloadReader(byte[] buffer)
    {
        _buffer = buffer ?? Array.Empty<byte>();
        _index = 0;
    }

    public int Remaining => _buffer.Length - _index;

    public bool TryReadByte(out byte value)
    {
        value = 0;
        if (Remaining < 1)
        {
            return false;
        }

        value = _buffer[_index];
        _index++;
        return true;
    }

    public bool TryReadUInt16LittleEndian(out ushort value)
    {
        value = 0;
        if (Remaining < 2)
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.AsSpan(_index, 2));
        _index += 2;
        return true;
    }

    public bool TryReadInt32LittleEndian(out int value)
    {
        value = 0;
        if (Remaining < 4)
        {
            return false;
        }

        value = BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan(_index, 4));
        _index += 4;
        return true;
    }

    public bool TryReadInt64LittleEndian(out long value)
    {
        value = 0;
        if (Remaining < 8)
        {
            return false;
        }

        value = BinaryPrimitives.ReadInt64LittleEndian(_buffer.AsSpan(_index, 8));
        _index += 8;
        return true;
    }

    public bool TryReadBytes(int count, out ReadOnlyMemory<byte> slice)
    {
        slice = default;
        if (count < 0 || Remaining < count)
        {
            return false;
        }

        slice = _buffer.AsMemory(_index, count);
        _index += count;
        return true;
    }

    /// <summary>剩余未读字节（常用于末尾变长字符串或原始块）。</summary>
    public ReadOnlyMemory<byte> ReadToEnd()
    {
        var rest = _buffer.AsMemory(_index, Remaining);
        _index = _buffer.Length;
        return rest;
    }
}
