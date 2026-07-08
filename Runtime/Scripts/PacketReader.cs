using System;
using System.Text;

namespace LANServiceDiscovery.Runtime
{
    /// <summary>
    /// 数据包游标读取器，实现 <see cref="IReader"/>。
    /// </summary>
    public class PacketReader : IReader
    {
        public byte Cmd { get; private set; }
        public int Available => _data.Length - _position;

        private byte[] _data = Array.Empty<byte>();
        private int _position;

        public PacketReader() { }

        public PacketReader(byte cmd, byte[] data)
        {
            Reset(cmd, data);
        }

        /// <summary>重用实例，替换为新的帧数据</summary>
        public void Reset(byte cmd, byte[] data)
        {
            Cmd = cmd;
            _data = data ?? Array.Empty<byte>();
            _position = 0;
        }

        public byte ReadByte()
        {
            CheckAvailable(1);
            return _data[_position++];
        }

        public short ReadInt16()
        {
            CheckAvailable(2);
            short val = (short)((_data[_position] << 8) | _data[_position + 1]);
            _position += 2;
            return val;
        }

        public int ReadInt32()
        {
            CheckAvailable(4);
            int val = (_data[_position] << 24) | (_data[_position + 1] << 16)
                    | (_data[_position + 2] << 8) | _data[_position + 3];
            _position += 4;
            return val;
        }

        public string ReadString()
        {
            int len = ReadInt16();
            CheckAvailable(len);
            string s = Encoding.UTF8.GetString(_data, _position, len);
            _position += len;
            return s;
        }

        public string ReadRemainingString()
        {
            return Encoding.UTF8.GetString(_data, _position, _data.Length - _position);
        }

        public byte[] ReadRemainingBytes()
        {
            int len = _data.Length - _position;
            byte[] result = new byte[len];
            Buffer.BlockCopy(_data, _position, result, 0, len);
            _position = _data.Length;
            return result;
        }

        private void CheckAvailable(int need)
        {
            if (_position + need > _data.Length)
                throw new IndexOutOfRangeException(
                    $"数据不足：需要 {need} 字节，剩余 {_data.Length - _position} 字节");
        }
    }
}
