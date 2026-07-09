using System;

namespace LANServiceDiscovery.Runtime
{
    /// <summary>
    /// 数据包读取器接口。
    /// <para>
    /// 封装解码后的单个协议帧（命令码 + 负载数据），提供游标式顺序读取方法。
    /// 读取操作从当前游标位置开始，每读一个字段游标自动前进。
    /// </para>
    /// <para>
    /// <b>可替换性：</b>继承 <see cref="UdpDiscoveryHostBase"/> 或 <see cref="UdpDiscoveryClientBase"/> 后，
    /// 通过 <c>Reader = new MyJsonReader()</c> 替换默认的 <see cref="PacketReader"/> 实现。
    /// </para>
    /// </summary>
    public interface IReader
    {
        /// <summary>当前帧的命令码（1 字节）</summary>
        byte Cmd { get; }

        /// <summary>剩余未读字节数</summary>
        int Available { get; }

        /// <summary>读取 1 字节，游标 +1</summary>
        byte ReadByte();

        /// <summary>读取 2 字节大端有符号整数，游标 +2</summary>
        short ReadInt16();

        /// <summary>读取 4 字节大端有符号整数，游标 +4</summary>
        int ReadInt32();

        /// <summary>
        /// 读取长度前缀字符串。
        /// 格式：前 2 字节大端表示 UTF-8 字节长度，后跟对应长度的 UTF-8 数据。
        /// 游标前进 2 + 字符串字节数。
        /// </summary>
        string ReadString();

        /// <summary>读取剩余全部数据，按 UTF-8 解码为字符串</summary>
        string ReadRemainingString();

        /// <summary>读取剩余全部原始字节</summary>
        byte[] ReadRemainingBytes();
    }
}
