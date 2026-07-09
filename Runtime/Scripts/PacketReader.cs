using System;
using System.Text;

namespace LANServiceDiscovery.Runtime
{
    /// <summary>
    /// 数据包游标读取器，默认的 <see cref="IReader"/> 实现。
    /// <para>
    /// 内部维护一个读取游标 <c>_position</c>，每次 Read 操作从当前位置读取并前进。
    /// 支持 <see cref="Reset"/> 方法重用实例，避免频繁分配内存。
    /// </para>
    /// <para>
    /// <b>数据格式约定：</b>所有多字节整数均为<b>大端序（网络字节序）</b>；
    /// 字符串格式为 2 字节大端长度前缀 + UTF-8 编码数据。
    /// </para>
    /// </summary>
    public class PacketReader : IReader
    {
        /// <summary>当前帧的命令码</summary>
        public byte Cmd { get; private set; }

        /// <summary>剩余可读字节数 = 总长度 - 当前游标位置</summary>
        public int Available => _data.Length - _position;

        /// <summary>负载数据缓冲区</summary>
        private byte[] _data = Array.Empty<byte>();

        /// <summary>当前读取游标位置（下一次读取的起始偏移）</summary>
        private int _position;

        /// <summary>创建空读取器，使用前需调用 <see cref="Reset"/></summary>
        public PacketReader() { }

        /// <summary>创建读取器并绑定帧数据</summary>
        /// <param name="cmd">命令码</param>
        /// <param name="data">负载数据</param>
        public PacketReader(byte cmd, byte[] data)
        {
            Reset(cmd, data);
        }

        /// <summary>
        /// 重置读取器，绑定到新的帧数据上。
        /// 命令码和游标位置会重置，旧的 <see cref="IReader.Available"/> 状态被清空。
        /// </summary>
        /// <param name="cmd">新帧的命令码</param>
        /// <param name="data">新帧的负载数据</param>
        public void Reset(byte cmd, byte[] data)
        {
            Cmd = cmd;
            _data = data ?? Array.Empty<byte>();
            _position = 0;
        }

        /// <summary>读取 1 字节，游标 +1</summary>
        public byte ReadByte()
        {
            CheckAvailable(1);
            return _data[_position++];
        }

        /// <summary>读取 2 字节大端 short，游标 +2</summary>
        public short ReadInt16()
        {
            CheckAvailable(2);
            short val = (short)((_data[_position] << 8) | _data[_position + 1]);
            _position += 2;
            return val;
        }

        /// <summary>读取 4 字节大端 int，游标 +4</summary>
        public int ReadInt32()
        {
            CheckAvailable(4);
            int val = (_data[_position] << 24) | (_data[_position + 1] << 16)
                    | (_data[_position + 2] << 8) | _data[_position + 3];
            _position += 4;
            return val;
        }

        /// <summary>
        /// 读取长度前缀字符串（2 字节大端长度 + UTF-8 数据），游标前进 2 + 字符串字节数。
        /// </summary>
        public string ReadString()
        {
            int len = ReadInt16();
            CheckAvailable(len);
            string s = Encoding.UTF8.GetString(_data, _position, len);
            _position += len;
            return s;
        }

        /// <summary>
        /// 读取游标位置之后的所有剩余字节，按 UTF-8 解码为字符串。
        /// 通常用于最后一帧数据（如 IP 地址）。
        /// </summary>
        public string ReadRemainingString()
        {
            return Encoding.UTF8.GetString(_data, _position, _data.Length - _position);
        }

        /// <summary>读取游标位置之后的所有剩余原始字节</summary>
        public byte[] ReadRemainingBytes()
        {
            int len = _data.Length - _position;
            byte[] result = new byte[len];
            Buffer.BlockCopy(_data, _position, result, 0, len);
            _position = _data.Length;
            return result;
        }

        /// <summary>
        /// 校验剩余数据是否足够读取指定字节数，不足时抛出异常。
        /// </summary>
        /// <param name="need">需要读取的字节数</param>
        /// <exception cref="IndexOutOfRangeException">数据不足时抛出</exception>
        private void CheckAvailable(int need)
        {
            if (_position + need > _data.Length)
                throw new IndexOutOfRangeException(
                    $"数据不足：需要 {need} 字节，剩余 {_data.Length - _position} 字节");
        }
    }
}
