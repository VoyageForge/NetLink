using System.Collections.Generic;
using System.Text;

namespace LANServiceDiscovery.Runtime
{
    /// <summary>
    /// 数据包构建器，实现 <see cref="IWriter"/>。链式写入后调用 <see cref="Encode(byte)"/> 产出完整帧。
    /// </summary>
    public class PacketWriter : IWriter
    {
        private readonly List<byte> _data = new List<byte>();

        /// <summary>清空已写入数据，重用实例</summary>
        public void Reset() => _data.Clear();

        public IWriter WriteByte(byte value)
        {
            _data.Add(value);
            return this;
        }

        public IWriter WriteInt16(short value)
        {
            _data.Add((byte)(value >> 8));
            _data.Add((byte)(value & 0xFF));
            return this;
        }

        public IWriter WriteInt32(int value)
        {
            _data.Add((byte)(value >> 24));
            _data.Add((byte)((value >> 16) & 0xFF));
            _data.Add((byte)((value >> 8) & 0xFF));
            _data.Add((byte)(value & 0xFF));
            return this;
        }

        public IWriter WriteString(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            WriteInt16((short)bytes.Length);
            _data.AddRange(bytes);
            return this;
        }

        public IWriter WriteBytes(byte[] value)
        {
            if (value != null && value.Length > 0)
                _data.AddRange(value);
            return this;
        }

        public byte[] Encode(byte cmd)
        {
            return PacketCodec.Encode(cmd, _data.ToArray());
        }

        public byte[] Encode(DiscoveryOpcode cmd)
        {
            return PacketCodec.Encode((byte)cmd, _data.ToArray());
        }
    }
}
