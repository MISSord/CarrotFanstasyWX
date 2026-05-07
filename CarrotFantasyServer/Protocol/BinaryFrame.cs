using System.Buffers.Binary;

namespace CarrotFantasyServer.Protocol;

/// <summary>
/// 一条 WebSocket Binary 消息 = 一帧：
/// <list type="bullet">
/// <item><description>字节 0–1：<c>ushort</c> 协议号，<strong>小端序</strong>（与 C# <see cref="BinaryPrimitives"/> / Unity <c>BitConverter</c> 在 little-endian 上一致）。</description></item>
/// <item><description>字节 2..：负载，语义由协议号定义（原始二进制、UTF-8、自定义 structs 序列化等）。</description></item>
/// </list>
/// 帧总长 = 2 + 负载长度；负载长度不再单独编码（由 WebSocket 帧边界确定）。
/// </summary>
public static class BinaryFrame
{
    public const int OpcodeSize = 2;

    public static bool TryDecode(byte[] packet, out ushort opcode, out byte[] payload)
    {
        opcode = 0;
        payload = Array.Empty<byte>();
        if (packet == null || packet.Length < OpcodeSize)
        {
            return false;
        }

        opcode = BinaryPrimitives.ReadUInt16LittleEndian(packet);
        int len = packet.Length - OpcodeSize;
        if (len == 0)
        {
            payload = Array.Empty<byte>();
        }
        else
        {
            payload = new byte[len];
            Buffer.BlockCopy(packet, OpcodeSize, payload, 0, len);
        }

        return true;
    }

    public static byte[] Encode(ushort opcode, ReadOnlySpan<byte> payload = default)
    {
        byte[] packet = new byte[OpcodeSize + payload.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(0, OpcodeSize), opcode);
        payload.CopyTo(packet.AsSpan(OpcodeSize));
        return packet;
    }

    public static byte[] Encode(ushort opcode, byte[]? payload)
    {
        if (payload == null || payload.Length == 0)
        {
            return Encode(opcode, ReadOnlySpan<byte>.Empty);
        }

        return Encode(opcode, payload.AsSpan());
    }
}
