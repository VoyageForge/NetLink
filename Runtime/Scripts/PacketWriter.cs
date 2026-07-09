using System.Collections.Generic;
using System.Text;

namespace LANServiceDiscovery.Runtime
{
    /// <summary>
    /// 数据包构建器，默认的 <see cref="IWriter"/> 实现。
    /// <para>
    /// 内部维护一个字节列表 <c>_data</c>，所有 Write 方法将数据追加到末尾。
    /// 支持 <see cref="Reset"/> 方法清空数据以重用实例。
    /// 最后调用 <see cref="Encode(byte)"/> 将已写入数据按 <see cref="PacketCodec"/> 协议打包为完整帧。
    /// </para>
    /// <para>
    /// <b>数据格式约定：</b>所有多字节整数写入为<b>大端序（网络字节序）</b>；
    /// 字符串写入格式为 2 字节大端长度前缀 + UTF-8 编码数据。
    /// </para>
    /// <para>
    /// <b>典型用法：</b>
    /// <code>
    /// byte[] frame = new PacketWriter()
    ///     .WriteString("192.168.1.1")
    ///     .WriteInt32(9999)
    ///     .Encode(DiscoveryOpcode.DiscoveryReply);
    /// </code>
    /// </para>
    /// </summary>
    public class PacketWriter : IWriter
    {
        /// <summary>已写入的负载数据缓冲区</summary>
        private readonly List<byte> _data = new List<byte>();

        /// <summary>清空所有已写入数据，将构建器恢复到初始状态以便重用</summary>
        public void Reset() => _data.Clear();

        /// <summary>写入 1 字节</summary>
        public IWriter WriteByte(byte value)
        {
            _data.Add(value);
            return this;
        }

        /// <summary>写入 2 字节大端 short</summary>
        public IWriter WriteInt16(short value)
        {
            _data.Add((byte)(value >> 8));      // 高字节
            _data.Add((byte)(value & 0xFF));    // 低字节
            return this;
        }

        /// <summary>写入 4 字节大端 int</summary>
        public IWriter WriteInt32(int value)
        {
            _data.Add((byte)(value >> 24));         // 最高字节
            _data.Add((byte)((value >> 16) & 0xFF));
            _data.Add((byte)((value >> 8) & 0xFF));
            _data.Add((byte)(value & 0xFF));        // 最低字节
            return this;
        }

        /// <summary>
        /// 写入长度前缀字符串。
        /// 先写入 2 字节大端 UTF-8 字节长度，再写入 UTF-8 编码数据。
        /// </summary>
        public IWriter WriteString(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            WriteInt16((short)bytes.Length);    // 长度前缀
            _data.AddRange(bytes);              // 字符串数据
            return this;
        }

        /// <summary>写入原始字节数组（不含长度前缀）</summary>
        public IWriter WriteBytes(byte[] value)
        {
            if (value != null && value.Length > 0)
                _data.AddRange(value);
            return this;
        }

        /// <summary>用指定命令码将已写入数据编码为完整的 <see cref="PacketCodec"/> 协议帧</summary>
        /// <param name="cmd">命令码（1 字节），用于自定义操作码扩展</param>
        /// <returns>完整的协议帧字节数组（含 SOF / 长度 / 校验和 / EOF）</returns>
        public byte[] Encode(byte cmd)
        {
            return PacketCodec.Encode(cmd, _data.ToArray());
        }

        /// <summary>用内置操作码将已写入数据编码为完整的协议帧</summary>
        public byte[] Encode(DiscoveryOpcode cmd)
        {
            return PacketCodec.Encode((byte)cmd, _data.ToArray());
        }
    }
}
