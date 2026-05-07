using System;
using System.Collections.Generic;
using System.Text;

namespace CarrotFantasy
{
    /// <summary>
    /// 构造负载字节：按序写入小端字段（与服务端解析顺序一致）。
    /// </summary>
    public sealed class ConnectionBinaryPayloadWriter
    {
        private readonly List<byte> bytes = new List<byte>();

        private static void AppendUInt16LittleEndian(List<byte> list, ushort value)
        {
            list.Add((byte)(value & 0xFF));
            list.Add((byte)(value >> 8));
        }

        private static void AppendInt32LittleEndian(List<byte> list, int value)
        {
            list.Add((byte)(value & 0xFF));
            list.Add((byte)((value >> 8) & 0xFF));
            list.Add((byte)((value >> 16) & 0xFF));
            list.Add((byte)((value >> 24) & 0xFF));
        }

        public ConnectionBinaryPayloadWriter WriteUInt16LittleEndian(ushort value)
        {
            AppendUInt16LittleEndian(this.bytes, value);
            return this;
        }

        public ConnectionBinaryPayloadWriter WriteInt32LittleEndian(int value)
        {
            AppendInt32LittleEndian(this.bytes, value);
            return this;
        }

        public ConnectionBinaryPayloadWriter WriteInt64LittleEndian(long value)
        {
            ulong u = unchecked((ulong)value);
            this.bytes.Add((byte)(u & 0xFF));
            this.bytes.Add((byte)((u >> 8) & 0xFF));
            this.bytes.Add((byte)((u >> 16) & 0xFF));
            this.bytes.Add((byte)((u >> 24) & 0xFF));
            this.bytes.Add((byte)((u >> 32) & 0xFF));
            this.bytes.Add((byte)((u >> 40) & 0xFF));
            this.bytes.Add((byte)((u >> 48) & 0xFF));
            this.bytes.Add((byte)((u >> 56) & 0xFF));
            return this;
        }

        public ConnectionBinaryPayloadWriter WriteBytes(byte[] data)
        {
            if (data != null && data.Length > 0)
            {
                this.bytes.AddRange(data);
            }

            return this;
        }

        public ConnectionBinaryPayloadWriter WriteUtf8(string text)
        {
            text ??= string.Empty;
            byte[] raw = Encoding.UTF8.GetBytes(text);
            return this.WriteBytes(raw);
        }

        public byte[] ToArray()
        {
            return this.bytes.ToArray();
        }
    }
}
