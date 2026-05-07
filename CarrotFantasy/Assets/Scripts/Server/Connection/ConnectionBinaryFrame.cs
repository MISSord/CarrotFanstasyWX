using System;

namespace CarrotFantasy
{
    /// <summary>
    /// WebSocket Binary 一帧：2 字节小端 ushort 协议号 + 负载（与 CarrotFantasyServer.BinaryFrame 一致）。
    /// </summary>
    public static class ConnectionBinaryFrame
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

            opcode = BitConverter.ToUInt16(packet, 0);
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

        public static byte[] Encode(ushort opcode, byte[] payload)
        {
            payload ??= Array.Empty<byte>();
            byte[] packet = new byte[OpcodeSize + payload.Length];
            byte[] opcodeBytes = BitConverter.GetBytes(opcode);
            packet[0] = opcodeBytes[0];
            packet[1] = opcodeBytes[1];
            if (payload.Length > 0)
            {
                Buffer.BlockCopy(payload, 0, packet, OpcodeSize, payload.Length);
            }

            return packet;
        }
    }
}
