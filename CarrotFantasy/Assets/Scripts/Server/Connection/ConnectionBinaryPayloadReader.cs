using System;

namespace CarrotFantasy
{
    /// <summary>
    /// 在负载字节内按序读取小端字段（与服务端 BinaryPayloadReader 约定一致）。
    /// </summary>
    public sealed class ConnectionBinaryPayloadReader
    {
        private readonly byte[] buffer;
        private int index;

        public ConnectionBinaryPayloadReader(byte[] buffer)
        {
            this.buffer = buffer ?? Array.Empty<byte>();
            this.index = 0;
        }

        public int Remaining => this.buffer.Length - this.index;

        public bool TryReadByte(out byte value)
        {
            value = 0;
            if (this.Remaining < 1)
            {
                return false;
            }

            value = this.buffer[this.index];
            this.index++;
            return true;
        }

        public bool TryReadUInt16LittleEndian(out ushort value)
        {
            value = 0;
            if (this.Remaining < 2)
            {
                return false;
            }

            value = BitConverter.ToUInt16(this.buffer, this.index);
            this.index += 2;
            return true;
        }

        public bool TryReadInt32LittleEndian(out int value)
        {
            value = 0;
            if (this.Remaining < 4)
            {
                return false;
            }

            value = BitConverter.ToInt32(this.buffer, this.index);
            this.index += 4;
            return true;
        }

        public bool TryReadInt64LittleEndian(out long value)
        {
            value = 0;
            if (this.Remaining < 8)
            {
                return false;
            }

            value = BitConverter.ToInt64(this.buffer, this.index);
            this.index += 8;
            return true;
        }

        public bool TryReadBytes(int count, out byte[] slice)
        {
            slice = null;
            if (count < 0 || this.Remaining < count)
            {
                return false;
            }

            slice = new byte[count];
            Buffer.BlockCopy(this.buffer, this.index, slice, 0, count);
            this.index += count;
            return true;
        }

        public byte[] ReadToEnd()
        {
            int len = this.Remaining;
            if (len == 0)
            {
                return Array.Empty<byte>();
            }

            byte[] rest = new byte[len];
            Buffer.BlockCopy(this.buffer, this.index, rest, 0, len);
            this.index = this.buffer.Length;
            return rest;
        }
    }
}
